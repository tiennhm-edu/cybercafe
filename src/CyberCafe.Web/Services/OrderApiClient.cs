// ============================================================================
// OrderApiClient.cs — typed HttpClient cho /api/orders (Buổi 32–35 · thay OrderStore in-memory).
// Đơn hàng giờ nằm trong SQL Server (qua Api) → restart Web không mất đơn,
// và quầy barista ở máy khác vẫn thấy cùng dữ liệu.
// Buổi 42–47: mọi endpoint đơn hàng cần đăng nhập → AuthSession + BearerTokenHandler gắn token.
// ============================================================================
using System.Net;
using CyberCafe.Contracts.Common;
using CyberCafe.Contracts.Orders;
using CyberCafe.Domain.Orders;
using CyberCafe.Web.Services.Auth;

namespace CyberCafe.Web.Services;

/// <summary>Đặt hàng / đọc đơn / đổi trạng thái qua CyberCafe.Api.</summary>
public class OrderApiClient(HttpClient http, AuthSession? session = null) : ApiClientBase(http, session)
{
    /// <summary>GET /api/orders/mine — đơn của khách đang đăng nhập (Buổi 42–47).</summary>
    public async Task<IReadOnlyList<OrderDto>> GetMineAsync(CancellationToken ct = default)
    {
        PagedResult<OrderDto> page = await this.SendAsync<PagedResult<OrderDto>>(
            new HttpRequestMessage(HttpMethod.Get, "api/orders/mine?pageSize=50"), ct);
        return page.Items;
    }

    /// <summary>POST /api/orders — trả về đơn đã lưu (có Id, tổng tiền do server tính).</summary>
    public Task<OrderDto> PlaceOrderAsync(PlaceOrderRequest request, CancellationToken ct = default) =>
        this.SendAsync<OrderDto>(JsonRequest(HttpMethod.Post, "api/orders", request), ct);

    /// <summary>GET /api/orders/{id}; không có → null.</summary>
    public async Task<OrderDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        try
        {
            return await this.SendAsync<OrderDto>(new HttpRequestMessage(HttpMethod.Get, $"api/orders/{id}"), ct);
        }
        catch (ApiException ex) when (ex.Status == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <summary>Các đơn đang chạy (Pending/Preparing/Ready) cho màn hình barista.</summary>
    public async Task<IReadOnlyList<OrderDto>> GetActiveAsync(CancellationToken ct = default)
    {
        PagedResult<OrderDto> page = await this.SendAsync<PagedResult<OrderDto>>(
            new HttpRequestMessage(HttpMethod.Get, "api/orders?pageSize=100"), ct);
        return page.Items;
    }

    /// <summary>PUT /api/orders/{id}/status — 409 nếu bước chuyển không hợp lệ.</summary>
    public Task<OrderDto> ChangeStatusAsync(int id, OrderStatus status, CancellationToken ct = default) =>
        this.SendAsync<OrderDto>(
            JsonRequest(HttpMethod.Put, $"api/orders/{id}/status", new ChangeOrderStatusRequest { Status = status }), ct);

    /// <summary>POST /api/orders/{id}/cancel.</summary>
    public Task<OrderDto> CancelAsync(int id, CancellationToken ct = default) =>
        this.SendAsync<OrderDto>(new HttpRequestMessage(HttpMethod.Post, $"api/orders/{id}/cancel"), ct);
}
