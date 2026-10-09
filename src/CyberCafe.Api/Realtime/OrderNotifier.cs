// ============================================================================
// OrderNotifier.cs — gửi sự kiện SignalR từ BÊN NGOÀI hub (Buổi 33–34 · IHubContext).
// Controller không phải hub, không có Clients → xin IHubContext<OrderHub, IOrderClient> qua DI.
// Bọc sau interface IOrderNotifier để:
//   - Controller không phụ thuộc SignalR (đọc code dễ hơn).
//   - Test tích hợp thay bằng bản giả, kiểm tra "đã thông báo đúng đơn chưa".
// ============================================================================
using CyberCafe.Contracts.Orders;
using CyberCafe.Contracts.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace CyberCafe.Api.Realtime;

/// <summary>Thông báo realtime về đơn hàng.</summary>
public interface IOrderNotifier
{
    /// <summary>Báo quầy barista có đơn mới.</summary>
    Task OrderPlacedAsync(OrderDto order, CancellationToken ct = default);

    /// <summary>Báo barista + khách đang xem đơn: trạng thái vừa đổi.</summary>
    Task OrderStatusChangedAsync(OrderDto order, CancellationToken ct = default);
}

/// <summary>Cài đặt bằng SignalR.</summary>
public class SignalROrderNotifier(IHubContext<OrderHub, IOrderClient> hub, ILogger<SignalROrderNotifier> logger) : IOrderNotifier
{
    /// <inheritdoc />
    public Task OrderPlacedAsync(OrderDto order, CancellationToken ct = default) =>
        this.SafeSendAsync(() => hub.Clients.Group(OrderHubContract.BaristasGroup).OrderPlaced(order), order.Code);

    /// <inheritdoc />
    public Task OrderStatusChangedAsync(OrderDto order, CancellationToken ct = default) =>
        // Clients.Groups([...]): gửi tới nhiều group trong 1 lệnh
        this.SafeSendAsync(
            () => hub.Clients.Groups([OrderHubContract.BaristasGroup, OrderHubContract.OrderGroup(order.Id)]).OrderStatusChanged(order),
            order.Code);

    // Đơn ĐÃ được lưu DB rồi → gửi realtime thất bại thì chỉ log, KHÔNG trả lỗi 500 cho khách
    // (khách F5 vẫn thấy dữ liệu đúng). Realtime là "tiện ích", database mới là nguồn sự thật.
    private async Task SafeSendAsync(Func<Task> send, string orderCode)
    {
        try
        {
            await send();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không gửi được sự kiện SignalR cho đơn {OrderCode}", orderCode);
        }
    }
}
