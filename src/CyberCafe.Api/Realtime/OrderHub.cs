// ============================================================================
// OrderHub.cs — SignalR hub cho đơn hàng realtime (Buổi 33–34 · Hub, Group, strongly-typed hub;
//               Buổi 42–47 · kết nối phải có JWT, vào group theo vai trò, chống IDOR trên hub).
// Luồng:
//   1) Màn hình /barista kết nối hub → gọi JoinBaristas → vào group "baristas" (chỉ Barista/Admin).
//   2) Trang /orders/{id} của khách kết nối → gọi WatchOrder(id) → vào group "order-{id}" (chỉ chủ đơn/staff).
//   3) Khách POST /api/orders → controller (qua IOrderNotifier) gửi OrderPlaced tới "baristas".
//   4) Barista PUT /api/orders/{id}/status → gửi OrderStatusChanged tới "baristas" + "order-{id}".
// Hub chỉ lo "ai nghe gì"; thao tác nghiệp vụ vẫn đi qua REST (dễ validate, dễ test, dễ phân quyền).
// Token: WebSocket không gửi được header Authorization → client gửi ?access_token=... (xem Program.cs OnMessageReceived).
// ============================================================================
using CyberCafe.Api.Auth;
using CyberCafe.Api.Data;
using CyberCafe.Api.Data.Configurations;
using CyberCafe.Contracts.Orders;
using CyberCafe.Contracts.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

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
/// (giống controller) → không lưu state trong field; inject DbContext (Scoped) được.
/// </summary>
[Authorize] // Buổi 42–47: kết nối không có token hợp lệ → bị từ chối ngay lúc negotiate (401)
public class OrderHub(CyberCafeDbContext db, ILogger<OrderHub> logger) : Hub<IOrderClient>
{
    // 👉 Bước 9 (b40.md) · 👉 Bước 12 (b47.md)
    /// <summary>Client (màn hình quầy) xin vào group baristas — chỉ Barista/Admin.</summary>
    // [Authorize] trên METHOD của hub: kiểm tra Context.User mỗi lần gọi; sai quyền → client nhận HubException.
    [Authorize(Policy = Policies.ProcessOrders)]
    public async Task JoinBaristas()
    {
        // Context.ConnectionId: id của KẾT NỐI hiện tại (mỗi tab trình duyệt / mỗi lần reconnect là 1 id mới)
        await this.Groups.AddToGroupAsync(this.Context.ConnectionId, OrderHubContract.BaristasGroup);
        logger.LogInformation("Kết nối {ConnectionId} ({User}) vào quầy barista", this.Context.ConnectionId, this.Context.User?.Identity?.Name);
    }

    /// <summary>Client (trang xác nhận đơn) theo dõi 1 đơn — chỉ chủ đơn hoặc nhân viên.</summary>
    public async Task WatchOrder(int orderId)
    {
        // IDOR trên hub: không kiểm tra thì khách A gọi WatchOrder(6) là nghe trộm được đơn của khách B.
        if (this.Context.User is null || !this.Context.User.IsStaff())
        {
            // Chỉ lấy đúng 1 cột UserId (projection), không tải cả đơn
            int? owner = await db.Orders
                .Where(o => o.Id == orderId)
                .Select(o => EF.Property<int?>(o, OrderConfiguration.OwnerUserId))
                .FirstOrDefaultAsync();
            if (owner is null || owner != this.Context.User?.GetUserId())
            {
                // HubException: thông báo này được gửi nguyên văn về client (exception khác thì bị che đi)
                throw new HubException("Không tìm thấy đơn hàng");
            }
        }

        await this.Groups.AddToGroupAsync(this.Context.ConnectionId, OrderHubContract.OrderGroup(orderId));
    }

    // Không cần RemoveFromGroup khi client ngắt kết nối: SignalR tự dọn group của connection đã đóng.
    // ⚠️ Lỗi hay gặp: client tự reconnect thì có ConnectionId MỚI → group cũ mất → client phải gọi lại
    //    JoinBaristas/WatchOrder trong sự kiện Reconnected (xem Barista.razor).
}
