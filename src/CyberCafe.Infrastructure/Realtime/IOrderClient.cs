// ============================================================================
// IOrderClient.cs — method SERVER gọi xuống CLIENT của hub đơn hàng (Buổi 33–34 · 48).
// Buổi 33–34: interface này nằm trong Api/Realtime/OrderHub.cs.
// Buổi 48: chuyển sang Infrastructure vì SignalROrderNotifier (Infrastructure) cần nó, còn OrderHub (Api)
//   vẫn dùng được (Api tham chiếu Infrastructure). Ngược lại thì không: Infrastructure không thấy Api.
// ============================================================================
using CyberCafe.Contracts.Orders;
using CyberCafe.Contracts.Realtime;

namespace CyberCafe.Infrastructure.Realtime;

/// <summary>
/// Hub&lt;IOrderClient&gt; (strongly-typed): gõ sai tên → lỗi biên dịch,
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
