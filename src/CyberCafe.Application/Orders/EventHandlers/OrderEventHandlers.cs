// ============================================================================
// OrderEventHandlers.cs — phản ứng với domain event của Order (Buổi 50 · domain event handler).
// b48: OrderService tự gọi notifier.OrderPlacedAsync(...) ngay sau SaveChanges, ở 2 chỗ khác nhau.
// b50: aggregate chỉ RAISE sự kiện; ai quan tâm thì đăng ký handler ở đây. Thêm phản ứng mới (gửi email,
//   tặng voucher khi đơn thứ 10...) = thêm 1 class handler, KHÔNG sửa Order hay command handler nào.
// Handler chạy SAU khi dữ liệu đã lưu (và transaction đã commit) → không bao giờ báo đơn "ma".
// ============================================================================
using CyberCafe.Application.Common.Messaging;
using CyberCafe.Domain.Orders.Events;
using Microsoft.Extensions.Logging;

namespace CyberCafe.Application.Orders.EventHandlers;

// 👉 Bước 7 (b50.md)
/// <summary>OrderPlaced → báo quầy barista (SignalR qua IOrderNotifier).</summary>
public sealed class NotifyBaristasOnOrderPlaced(IOrderNotifier notifier) : IDomainEventHandler<OrderPlaced>
{
    /// <inheritdoc />
    public Task Handle(OrderPlaced domainEvent, CancellationToken ct) =>
        notifier.OrderPlacedAsync(domainEvent.Order.ToDto(), ct); // Id đã có: event phát SAU SaveChanges
}

/// <summary>OrderStatusChanged → báo quầy + khách đang mở trang đơn đó.</summary>
public sealed class NotifyOnOrderStatusChanged(IOrderNotifier notifier) : IDomainEventHandler<OrderStatusChanged>
{
    /// <inheritdoc />
    public Task Handle(OrderStatusChanged domainEvent, CancellationToken ct) =>
        notifier.OrderStatusChangedAsync(domainEvent.Order.ToDto(), ct);
}

/// <summary>
/// OrderPaid → ghi log doanh thu. Ví dụ "1 event, nhiều phản ứng độc lập": cùng lúc Pay() còn có OrderPlaced
/// (báo barista). Chỗ này sau có thể thành: xuất hóa đơn điện tử, gửi email biên nhận...
/// </summary>
public sealed class LogOrderPaid(ILogger<LogOrderPaid> logger) : IDomainEventHandler<OrderPaid>
{
    /// <inheritdoc />
    public Task Handle(OrderPaid domainEvent, CancellationToken ct)
    {
        // Chỉ log phương thức + số tiền; KHÔNG log Payment.Display() (có 4 số cuối thẻ / SĐT MoMo)
        logger.LogInformation("Đơn {Code} đã thanh toán {Amount} qua {Method}",
            domainEvent.Order.Code, domainEvent.Amount, domainEvent.Order.Payment?.Method);
        return Task.CompletedTask;
    }
}
