// ============================================================================
// OrderPlacedConsumer.cs — Payment service nhận "có đơn cần thanh toán" từ RabbitMQ (Buổi 55 · idempotent consumer).
// Các bước cho MỖI message:
//   1) Đã xử lý chưa? (EventId trong ProcessedMessages HOẶC đơn này đã có giao dịch) → có: gửi LẠI kết quả cũ, dừng.
//   2) Chờ giả lập + áp luật (PaymentRules).
//   3) Lưu giao dịch + đánh dấu EventId — 1 lần SaveChanges = 1 transaction.
//   4) Publish PaymentCompleted / PaymentFailed (EventId đã lưu ở bước 3).
// Vì sao bước 1 GỬI LẠI chứ không im lặng? Nếu lần trước sập giữa bước 3 và 4 (đã lưu, chưa kịp gửi), RabbitMQ
// giao lại message → ta phải gửi kết quả, nếu không đơn bên Order service "chờ thanh toán" mãi mãi.
// Gửi lại với CÙNG ResultEventId → bên Order (inbox) nhận ra trùng nếu lần trước thật ra đã tới nơi.
// → Service này KHÔNG cần outbox: "lưu rồi gửi, trùng thì gửi lại" + bên nhận idempotent là đủ (at-least-once).
// ============================================================================
using CyberCafe.IntegrationEvents;
using CyberCafe.Payment.Api.Data;
using CyberCafe.Payment.Api.Processing;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CyberCafe.Payment.Api.Messaging;

// 👉 Bước 4 (b55.md) · 👉 Bước 5 (b55.md)
/// <summary>OrderPlacedIntegrationEvent → quyết định → lưu → PaymentCompleted/PaymentFailed.</summary>
public sealed class OrderPlacedConsumer(
    PaymentDbContext db,
    PaymentRules rules,
    IOptions<PaymentRulesOptions> options,
    TimeProvider clock,
    ILogger<OrderPlacedConsumer> logger) : IConsumer<OrderPlacedIntegrationEvent>
{
    /// <summary>Tên consumer trong bảng ProcessedMessages.</summary>
    public const string ConsumerName = nameof(OrderPlacedConsumer);

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<OrderPlacedIntegrationEvent> context)
    {
        // context.Message: record đã được MassTransit đọc từ JSON; CancellationToken hủy khi service đang dừng
        OrderPlacedIntegrationEvent message = context.Message;
        CancellationToken ct = context.CancellationToken;

        // 1) IDEMPOTENT: 2 lớp kiểm tra — theo EventId (inbox) và theo khóa nghiệp vụ OrderId (1 đơn = 1 giao dịch)
        PaymentTransaction? existing = await db.Transactions.SingleOrDefaultAsync(t => t.OrderId == message.OrderId, ct);
        bool seen = await db.ProcessedMessages.AnyAsync(m => m.MessageId == message.EventId && m.Consumer == ConsumerName, ct);
        if (existing is not null || seen)
        {
            logger.LogInformation("Message trùng cho đơn {OrderCode} (EventId {EventId}) — gửi lại kết quả cũ", message.OrderCode, message.EventId);
            if (existing is not null)
            {
                await PublishResultAsync(context, existing);
            }

            return;
        }

        // 2) Giả lập thời gian chờ ngân hàng + áp luật
        if (options.Value.SimulatedDelayMs > 0)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(options.Value.SimulatedDelayMs), clock, ct);
        }

        // Luật nằm trong class thuần (PaymentRules) → consumer chỉ "điều phối", test luật không cần bus
        PaymentDecision decision = rules.Decide(message.Amount, message.PaymentMethod, message.CardLast4);
        DateTime now = clock.GetUtcNow().UtcDateTime;

        // 3) Lưu giao dịch + đánh dấu đã xử lý trong CÙNG 1 SaveChanges (EF tự bọc 1 transaction)
        PaymentTransaction transaction = new()
        {
            OrderId = message.OrderId,
            OrderCode = message.OrderCode,
            Amount = message.Amount,
            Method = message.PaymentMethod,
            CardLast4 = message.CardLast4,
            Status = decision.Approved ? PaymentStatus.Succeeded : PaymentStatus.Failed,
            FailureReason = decision.Reason,
            TransactionId = $"PAY-{now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            RequestEventId = message.EventId,
            ResultEventId = Guid.NewGuid(),
            ProcessedAtUtc = now,
        };
        db.Transactions.Add(transaction);
        db.ProcessedMessages.Add(new ProcessedMessage { MessageId = message.EventId, Consumer = ConsumerName, ProcessedAtUtc = now });
        // ⚠️ 2 bản trùng chạy SONG SONG đều qua bước 1 → bản sau vi phạm UNIQUE(OrderId) / khóa chính ProcessedMessages
        //    → DbUpdateException → MassTransit retry → lần retry thấy "đã xử lý" ở bước 1. Database là trọng tài cuối cùng.
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Đơn {OrderCode}: {Status} {Amount} qua {Method} {Reason}",
            message.OrderCode, transaction.Status, message.Amount, message.PaymentMethod, decision.Reason);

        // 4) Báo kết quả cho Order service. Publish lỗi (broker vừa rớt) → exception → MassTransit retry message gốc
        //    → lần sau rơi vào nhánh "đã xử lý" ở bước 1 → gửi lại kết quả. Không mất, không thu tiền 2 lần.
        await PublishResultAsync(context, transaction);
    }

    // MessageId của broker = EventId của event → cùng 1 "chứng minh thư" ở mọi tầng
    private static Task PublishResultAsync(ConsumeContext context, PaymentTransaction tx) => tx.Status == PaymentStatus.Succeeded
        ? context.Publish(
            new PaymentCompletedIntegrationEvent(tx.ResultEventId, tx.ProcessedAtUtc, tx.OrderId, tx.Amount, tx.Method, tx.CardLast4, tx.TransactionId),
            publish => publish.MessageId = tx.ResultEventId)
        : context.Publish(
            new PaymentFailedIntegrationEvent(tx.ResultEventId, tx.ProcessedAtUtc, tx.OrderId, tx.FailureReason ?? "Thanh toán thất bại"),
            publish => publish.MessageId = tx.ResultEventId);
}
