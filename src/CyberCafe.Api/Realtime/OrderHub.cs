// ============================================================================
// OrderHub.cs — SignalR hub cho đơn hàng realtime (Buổi 33–34 · Hub, Group, strongly-typed hub).
// Luồng:
//   1) Màn hình /barista kết nối hub → gọi JoinBaristas → vào group "baristas".
//   2) Trang /orders/{id} của khách kết nối → gọi WatchOrder(id) → vào group "order-{id}".
//   3) Khách POST /api/orders → controller (qua IOrderNotifier) gửi OrderPlaced tới "baristas".
//   4) Barista PUT /api/orders/{id}/status → gửi OrderStatusChanged tới "baristas" + "order-{id}".
// Hub chỉ lo "ai nghe gì"; thao tác nghiệp vụ vẫn đi qua REST (dễ validate, dễ test, dễ phân quyền).
// ============================================================================
using CyberCafe.Contracts.Orders;
using CyberCafe.Contracts.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace CyberCafe.Api.Realtime;

/// <summary>
/// Method SERVER gọi xuống CLIENT. Hub&lt;IOrderClient&gt; (strongly-typed): gõ sai tên → lỗi biên dịch,
/// thay vì Clients.All.SendAsync("OrderPlace", ...) gõ sai mà không ai báo.
/// Tên method ở đây phải trùng hằng số trong <see cref="OrderHubContract"/> (client dùng hằng số đó).
/// </summary>
public interface IOrderClient
{
    /// <summary>Có đơn mới.</summary>
    Task OrderPlaced(OrderDto order);

    /// <summary>Đơn vừa đổi trạng thái.</summary>
    Task OrderStatusChanged(OrderDto order);
}

/// <summary>
/// Hub tại /hubs/orders. Mỗi lần client gọi method, SignalR tạo 1 instance hub MỚI
/// (giống controller) → không lưu state trong field của hub.
/// </summary>
public class OrderHub(ILogger<OrderHub> logger) : Hub<IOrderClient>
{
    // 👉 Bước 9 (b40.md)
    /// <summary>Client (màn hình quầy) xin vào group baristas.</summary>
    public async Task JoinBaristas()
    {
        // Context.ConnectionId: id của KẾT NỐI hiện tại (mỗi tab trình duyệt / mỗi lần reconnect là 1 id mới)
        await this.Groups.AddToGroupAsync(this.Context.ConnectionId, OrderHubContract.BaristasGroup);
        logger.LogInformation("Kết nối {ConnectionId} vào quầy barista", this.Context.ConnectionId);
    }

    /// <summary>Client (trang xác nhận đơn) theo dõi 1 đơn.</summary>
    public Task WatchOrder(int orderId)
    {
        // Buổi 42–47 sẽ kiểm tra: khách chỉ được theo dõi ĐƠN CỦA MÌNH.
        return this.Groups.AddToGroupAsync(this.Context.ConnectionId, OrderHubContract.OrderGroup(orderId));
    }

    // Không cần RemoveFromGroup khi client ngắt kết nối: SignalR tự dọn group của connection đã đóng.
    // ⚠️ Lỗi hay gặp: client tự reconnect thì có ConnectionId MỚI → group cũ mất → client phải gọi lại
    //    JoinBaristas/WatchOrder trong sự kiện Reconnected (xem Barista.razor).
}
