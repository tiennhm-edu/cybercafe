// ============================================================================
// DomainEventPublisher.cs — phát domain event tới các handler ở Application (Buổi 50 · domain event).
// Ai gọi? CyberCafeDbContext (Infrastructure) — SAU khi SaveChanges thành công (và sau khi transaction commit).
// Mỗi event có thể có 0..n handler (OrderPaid: ghi log; OrderPlaced: báo quầy barista...).
// Handler lỗi KHÔNG làm hỏng request: dữ liệu đã lưu rồi, side effect (SignalR, email) là "cố gắng hết sức".
//   Cần chắc chắn 100% (không mất thông báo kể cả khi server sập ngay sau commit) → Outbox pattern (b55).
// ============================================================================
using System.Collections.Concurrent;
using CyberCafe.Domain.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CyberCafe.Application.Common.Messaging;

// 👉 Bước 7 (b50.md)
/// <summary>Xử lý 1 loại domain event (ví dụ: OrderPlaced → báo barista).</summary>
/// <typeparam name="TEvent">Kiểu event.</typeparam>
public interface IDomainEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    /// <summary>Phản ứng với sự kiện đã xảy ra (chỉ ĐỌC aggregate trong event, không sửa).</summary>
    Task Handle(TEvent domainEvent, CancellationToken ct);
}

/// <summary>Cổng để Infrastructure phát event mà không cần biết có những handler nào.</summary>
public interface IDomainEventPublisher
{
    /// <summary>Phát lần lượt từng event tới mọi handler đã đăng ký.</summary>
    Task PublishAsync(IReadOnlyCollection<IDomainEvent> domainEvents, CancellationToken ct = default);
}

/// <summary>Cài đặt bằng DI: tìm IEnumerable&lt;IDomainEventHandler&lt;TEvent&gt;&gt; theo kiểu THẬT của event.</summary>
public sealed class DomainEventPublisher(IServiceProvider services, ILogger<DomainEventPublisher> logger) : IDomainEventPublisher
{
    // Cùng kỹ thuật wrapper như Sender: biết kiểu event lúc chạy → dựng EventWrapper<TEvent> 1 lần rồi cache
    private static readonly ConcurrentDictionary<Type, EventWrapper> Wrappers = new();

    /// <inheritdoc />
    public async Task PublishAsync(IReadOnlyCollection<IDomainEvent> domainEvents, CancellationToken ct = default)
    {
        foreach (IDomainEvent domainEvent in domainEvents)
        {
            EventWrapper wrapper = Wrappers.GetOrAdd(domainEvent.GetType(),
                static type => (EventWrapper)Activator.CreateInstance(typeof(EventWrapper<>).MakeGenericType(type))!);

            foreach ((string handlerName, Func<Task> run) in wrapper.Handlers(domainEvent, services, ct))
            {
                try
                {
                    await run();
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // ⚠️ Không ném tiếp: client nhận 500 dù đơn ĐÃ lưu → khách bấm đặt lại → đơn trùng.
                    logger.LogError(ex, "Handler {Handler} lỗi khi xử lý {Event}", handlerName, domainEvent.GetType().Name);
                }
            }
        }
    }

    private abstract class EventWrapper
    {
        public abstract IEnumerable<(string Name, Func<Task> Run)> Handlers(IDomainEvent domainEvent, IServiceProvider services, CancellationToken ct);
    }

    private sealed class EventWrapper<TEvent> : EventWrapper
        where TEvent : IDomainEvent
    {
        public override IEnumerable<(string Name, Func<Task> Run)> Handlers(IDomainEvent domainEvent, IServiceProvider services, CancellationToken ct) =>
            services.GetServices<IDomainEventHandler<TEvent>>()
                .Select(h => (h.GetType().Name, (Func<Task>)(() => h.Handle((TEvent)domainEvent, ct))));
    }
}
