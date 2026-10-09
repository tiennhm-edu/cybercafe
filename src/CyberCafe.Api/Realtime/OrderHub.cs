// ============================================================================
// OrderHub.cs — SignalR hub cho đơn hàng realtime (Buổi 33–34 · Hub, Group, strongly-typed hub;
//               Buổi 42–47 · kết nối phải có JWT, vào group theo vai trò, chống IDOR trên hub;
//               Buổi 48 · hub là "cửa vào" như controller → ở lại Api;
//               Buổi 51–53 · hỏi quyền bằng GetOrderByIdQuery — CÙNG luật IDOR với GET /api/orders/{id}).
// Luồng:
//   1) Màn hình /barista kết nối hub → gọi JoinBaristas → vào group "baristas" (chỉ Barista/Admin).
//   2) Trang /orders/{id} của khách kết nối → gọi WatchOrder(id) → vào group "order-{id}" (chỉ chủ đơn/staff).
//   3) Khách POST /api/orders → Order.Pay() raise OrderPlaced → (sau khi lưu) handler gửi tới "baristas".
//   4) Barista PUT /api/orders/{id}/status → OrderStatusChanged → gửi tới "baristas" + "order-{id}".
// Hub chỉ lo "ai nghe gì"; thao tác nghiệp vụ vẫn đi qua REST (dễ validate, dễ test, dễ phân quyền).
// Token: WebSocket không gửi được header Authorization → client gửi ?access_token=... (xem Program.cs OnMessageReceived).
// Buổi 48: IOrderClient chuyển sang Infrastructure/Realtime (adapter SignalROrderNotifier cần nó).
// ============================================================================
using CyberCafe.Api.Auth;
using CyberCafe.Application.Common.Messaging;
using CyberCafe.Application.Orders.Queries;
using CyberCafe.Contracts.Realtime;
using CyberCafe.Infrastructure.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CyberCafe.Api.Realtime;

/// <summary>
/// Hub tại /hubs/orders. Mỗi lần client gọi method, SignalR tạo 1 instance hub MỚI
/// (giống controller) → không lưu state trong field; inject service Scoped được.
/// </summary>
[Authorize] // Buổi 42–47: kết nối không có token hợp lệ → bị từ chối ngay lúc negotiate (401)
public class OrderHub(ISender sender, ILogger<OrderHub> logger) : Hub<IOrderClient>
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
        // 👉 Bước 8 (b48.md) · 👉 Bước 4 (b53.md): luật "ai được xem đơn" nằm TRONG query (WHERE ... UserId = @me)
        // — controller và hub gửi CÙNG GetOrderByIdQuery. Hub là "cửa vào" thứ 2, dispatcher dùng chung y hệt REST.
        if (this.Context.User is null
            || await sender.Send(new GetOrderByIdQuery(orderId, this.Context.User.ToCurrentUser())) is null)
        {
            // HubException: thông báo này được gửi nguyên văn về client (exception khác thì bị che đi)
            throw new HubException("Không tìm thấy đơn hàng");
        }

        await this.Groups.AddToGroupAsync(this.Context.ConnectionId, OrderHubContract.OrderGroup(orderId));
    }

    // Không cần RemoveFromGroup khi client ngắt kết nối: SignalR tự dọn group của connection đã đóng.
    // ⚠️ Lỗi hay gặp: client tự reconnect thì có ConnectionId MỚI → group cũ mất → client phải gọi lại
    //    JoinBaristas/WatchOrder trong sự kiện Reconnected (xem Barista.razor).
}
