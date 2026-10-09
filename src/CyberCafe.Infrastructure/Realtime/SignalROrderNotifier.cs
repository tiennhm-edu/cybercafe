// ============================================================================
// SignalROrderNotifier.cs — gửi sự kiện SignalR từ BÊN NGOÀI hub (Buổi 33–34 · 48 · IHubContext, adapter).
// Code nghiệp vụ không phải hub, không có Clients → xin IHubContext<THub, IOrderClient> qua DI.
// Bọc sau interface IOrderNotifier (Application) để:
//   - Use case (b48 OrderService, b50+ handler domain event) không phụ thuộc SignalR.
//   - Test tích hợp thay bằng bản giả, kiểm tra "đã thông báo đúng đơn chưa".
// Buổi 48: tách khỏi file chứa interface (b40: Api/Realtime/OrderNotifier.cs) và chuyển sang Infrastructure.
//   Vướng mắc: IHubContext<OrderHub, ...> cần KIỂU OrderHub, mà OrderHub là "cửa vào" nằm ở Api —
//   Infrastructure KHÔNG được tham chiếu Api (sẽ thành vòng tròn). Cách gỡ: class GENERIC theo THub.
//   Api (composition root) mới chọn THub = OrderHub: services.AddOrderNotifier<OrderHub>().
// ============================================================================
using CyberCafe.Application.Orders;
using CyberCafe.Contracts.Orders;
using CyberCafe.Contracts.Realtime;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace CyberCafe.Infrastructure.Realtime;

// 👉 Bước 9 (b48.md)
/// <summary>Cài đặt IOrderNotifier bằng SignalR cho bất kỳ hub nào kiểu Hub&lt;IOrderClient&gt;.</summary>
/// <typeparam name="THub">Hub thật (OrderHub ở Api) — chỉ dùng để DI tìm đúng IHubContext.</typeparam>
public class SignalROrderNotifier<THub>(IHubContext<THub, IOrderClient> hub, ILogger<SignalROrderNotifier<THub>> logger) : IOrderNotifier
    where THub : Hub<IOrderClient>
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
