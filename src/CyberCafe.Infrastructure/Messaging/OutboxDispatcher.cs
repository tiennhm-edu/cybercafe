// ============================================================================
// OutboxDispatcher.cs — đọc OutboxMessages chưa gửi → publish lên broker → đánh dấu đã gửi (Buổi 55).
//   OutboxDispatcher       : 1 lượt quét (Scoped, dùng DbContext) — test gọi thẳng, không cần chờ timer.
//   OutboxPublisherWorker  : BackgroundService gọi dispatcher mỗi N ms (chạy suốt đời tiến trình Api).
//   IIntegrationEventBus   : "gửi lên broker" — bản thật dùng MassTransit, test dùng bản giả.
// Thứ tự trong 1 message: PUBLISH trước, ghi ProcessedAtUtc SAU. Đảo lại (ghi trước, publish sau) mà sập ở giữa
// → dòng đã "gửi" nhưng broker chưa từng nhận → MẤT message, đúng thứ Outbox sinh ra để chống.
// Giới hạn (nói rõ với lớp): 2 instance Api cùng quét có thể gửi trùng 1 dòng → vẫn đúng nhờ consumer idempotent;
// muốn tránh hẳn: khóa dòng (UPDLOCK, READPAST) hoặc dùng outbox có sẵn của thư viện (MassTransit EF outbox).
// ============================================================================
using System.Diagnostics;
using CyberCafe.Infrastructure.Persistence;
using CyberCafe.IntegrationEvents;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CyberCafe.Infrastructure.Messaging;

/// <summary>Cổng gửi integration event lên broker (tách ra để test dispatcher không cần RabbitMQ).</summary>
public interface IIntegrationEventBus
{
    /// <summary>Publish 1 event (MessageId của broker = EventId để bên nhận lọc trùng).</summary>
    Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken ct = default);
}

// 👉 Bước 3 (b55.md)
/// <summary>Bản thật: MassTransit → RabbitMQ (exchange theo kiểu message, mọi queue đăng ký kiểu đó đều nhận).</summary>
public sealed class MassTransitIntegrationEventBus(IPublishEndpoint publishEndpoint) : IIntegrationEventBus
{
    /// <inheritdoc />
    public Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken ct = default) =>
        // Publish(object, Type, ...): kiểu THẬT lúc chạy (OrderPlacedIntegrationEvent), không phải interface
        publishEndpoint.Publish(
            integrationEvent,
            integrationEvent.GetType(),
            Pipe.Execute<PublishContext>(context => context.MessageId = integrationEvent.EventId),
            ct);
}

/// <summary>1 lượt quét outbox.</summary>
public sealed class OutboxDispatcher(
    CyberCafeDbContext db,
    IIntegrationEventBus bus,
    TimeProvider clock,
    ILogger<OutboxDispatcher> logger)
{
    /// <summary>Nguồn span "outbox publish ..." (ServiceDefaults đăng ký mọi nguồn "CyberCafe.*").</summary>
    public static readonly ActivitySource ActivitySource = new("CyberCafe.Outbox");

    /// <summary>Quá số lần này thì thôi thử (message "độc" — cần người xem LastError).</summary>
    public const int MaxAttempts = 10;

    // 👉 Bước 3 (b55.md)
    /// <summary>Gửi tối đa <paramref name="batchSize"/> message cũ nhất chưa gửi. Trả về số message gửi thành công.</summary>
    public async Task<int> DispatchPendingAsync(int batchSize = 20, CancellationToken ct = default)
    {
        // Lấy theo lô (batchSize) thay vì cả bảng: broker chết lâu → bảng có hàng nghìn dòng, không kéo hết vào RAM.
        // Attempts < MaxAttempts: message "độc" (JSON hỏng, kiểu đã đổi tên) không chặn mãi các message phía sau.
        List<OutboxMessage> batch = await db.OutboxMessages
            .Where(m => m.ProcessedAtUtc == null && m.Attempts < MaxAttempts)
            .OrderBy(m => m.OccurredAtUtc)
            .Take(batchSize)
            .ToListAsync(ct);

        int sent = 0;
        foreach (OutboxMessage message in batch)
        {
            // Nối trace: span mới là CON của request đã tạo message (traceparent lưu trong dòng outbox).
            ActivityContext.TryParse(message.TraceParent, null, out ActivityContext parent);
            using Activity? activity = ActivitySource.StartActivity(
                $"outbox publish {message.Type[(message.Type.LastIndexOf('.') + 1)..]}", ActivityKind.Producer, parent);

            try
            {
                // ToIntegrationEvent: JSON → đúng kiểu record ban đầu (Type lưu trong dòng) → MassTransit chọn đúng exchange
                await bus.PublishAsync(message.ToIntegrationEvent(), ct);
                message.ProcessedAtUtc = clock.GetUtcNow().UtcDateTime;
                sent++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Broker tắt / mất mạng: KHÔNG ném tiếp — giữ dòng lại, lượt sau thử tiếp (dữ liệu đơn hàng vẫn an toàn trong DB)
                message.Attempts++;
                message.LastError = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                logger.LogWarning(ex, "Gửi outbox {MessageId} thất bại (lần {Attempts})", message.Id, message.Attempts);
            }

            // Lưu SAU MỖI message: sập giữa batch thì chỉ gửi lại đúng message đang dở, không gửi lại cả batch
            await db.SaveChangesAsync(ct);
        }

        // Trả về số message gửi được: worker dùng để quyết định quét tiếp ngay (còn việc) hay chờ tick sau (hết việc)
        return sent;
    }
}

/// <summary>Chạy nền: cứ mỗi Outbox:PollingIntervalMs (mặc định 1000) lại quét outbox.</summary>
public sealed class OutboxPublisherWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<OutboxPublisherWorker> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Polling 1 giây: message đến Payment chậm tối đa ~1 giây — đổi lấy sự đơn giản (không cần trigger/CDC).
        // Test đặt 50 ms để không phải chờ.
        TimeSpan interval = TimeSpan.FromMilliseconds(configuration.GetValue("Outbox:PollingIntervalMs", 1000));
        using PeriodicTimer timer = new(interval);
        do
        {
            try
            {
                // BackgroundService là Singleton, DbContext là Scoped → tự tạo scope cho MỖI lượt quét.
                // ⚠️ Lỗi hay gặp: inject thẳng CyberCafeDbContext vào constructor của worker → lỗi "Cannot consume scoped service".
                using IServiceScope scope = scopeFactory.CreateScope();
                OutboxDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<OutboxDispatcher>();
                while (await dispatcher.DispatchPendingAsync(ct: stoppingToken) > 0)
                {
                    // còn message thì quét tiếp ngay, không chờ tick sau
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Lượt quét outbox lỗi — thử lại ở lượt sau");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
