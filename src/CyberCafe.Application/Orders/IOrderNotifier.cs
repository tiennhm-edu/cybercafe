// ============================================================================
// IOrderNotifier.cs — cổng thông báo realtime về đơn hàng (Buổi 33–34 · 48 · 50 · 55).
// Buổi 33–34: interface + bản SignalR nằm chung file Api/Realtime/OrderNotifier.cs.
// Buổi 48: TÁCH ĐÔI theo Clean Architecture:
//   - Interface (file này) ở Application: use case chỉ cần "báo cho quầy biết", không cần biết SignalR.
//     (b50: người gọi là handler domain event trong Orders/EventHandlers, không còn gọi tay sau SaveChanges.)
//   - Adapter SignalROrderNotifier<THub> ở Infrastructure/Realtime: dùng IHubContext để gửi thật.
// Test tích hợp vẫn thay bằng RecordingOrderNotifier y như cũ (chỉ đổi using).
// ============================================================================
using CyberCafe.Contracts.Orders;

namespace CyberCafe.Application.Orders;

// 👉 Bước 9 (b48.md)
/// <summary>Thông báo realtime về đơn hàng.</summary>
public interface IOrderNotifier
{
    /// <summary>Báo quầy barista có đơn mới (đã thanh toán); b55: kèm khách đang xem đơn đó.</summary>
    Task OrderPlacedAsync(OrderDto order, CancellationToken ct = default);

    /// <summary>Báo barista + khách đang xem đơn: trạng thái vừa đổi.</summary>
    Task OrderStatusChangedAsync(OrderDto order, CancellationToken ct = default);
}
