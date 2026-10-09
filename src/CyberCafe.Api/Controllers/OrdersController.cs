// ============================================================================
// OrdersController.cs — đặt hàng & xử lý đơn /api/orders (Buổi 32–35 · REST;
//                       Buổi 33–34 · thông báo SignalR; Buổi 36–41 · Include, tracking;
//                       Buổi 42–47 · phân quyền theo vai trò + chống IDOR;
//                       Buổi 48 · controller MỎNG; Buổi 51 · CQRS: mỗi action = gửi 1 command/query).
//   POST /api/orders                 Customer        201 | 400 | 409 (món tạm hết)          PlaceOrderCommand
//   GET  /api/orders/mine            Customer        200 — CHỈ đơn của mình                  GetMyOrdersQuery
//   GET  /api/orders?status=...      Barista/Admin   200 — mọi đơn (màn hình quầy)           GetBaristaBoardQuery
//   GET  /api/orders/{id}            chủ đơn / staff 200 | 404                               GetOrderByIdQuery
//   PUT  /api/orders/{id}/status     Barista/Admin   200 | 400 | 404 | 409                   ChangeOrderStatusCommand
//   POST /api/orders/{id}/cancel     chủ đơn (khi Pending) / staff   200 | 404 | 409         CancelOrderCommand
// Thiếu token → 401; có token nhưng sai vai trò → 403. Hợp đồng HTTP giữ nguyên từ b47.
// Buổi 51: controller chỉ phụ thuộc ISender — không biết handler nào xử lý, không biết domain event, transaction.
// ============================================================================
using CyberCafe.Api.Auth;
using CyberCafe.Application.Common.Messaging;
using CyberCafe.Application.Orders.Commands;
using CyberCafe.Application.Orders.Queries;
using CyberCafe.Contracts.Common;
using CyberCafe.Contracts.Orders;
using CyberCafe.Domain.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CyberCafe.Api.Controllers;

/// <summary>Đặt hàng và xử lý đơn.</summary>
[ApiController]
[Route("api/orders")]
[Authorize] // mọi endpoint đơn hàng đều cần đăng nhập; từng action siết thêm bằng policy
public class OrdersController(ISender sender) : ControllerBase
{
    // 👉 Bước 7 (b51.md)
    /// <summary>Đặt hàng (Customer): server tự tra giá, mã giảm giá, tính tiền và tạo thanh toán.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.PlaceOrders)]
    [ProducesResponseType<OrderDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderDto>> Place(PlaceOrderRequest request, CancellationToken ct)
    {
        // 👉 Bước 11 (b47.md): CHỦ ĐƠN = người trong token (KHÔNG lấy từ body — client gửi gì cũng được).
        OrderDto dto = await sender.Send(PlaceOrderCommand.From(request, this.User.GetUserId()), ct);
        return this.CreatedAtAction(nameof(this.GetById), new { id = dto.Id }, dto);
    }

    /// <summary>Đơn của chính khách đang đăng nhập, mới nhất trước.</summary>
    [HttpGet("mine")]
    [Authorize(Policy = Policies.PlaceOrders)]
    [ProducesResponseType<PagedResult<OrderDto>>(StatusCodes.Status200OK)]
    public Task<PagedResult<OrderDto>> Mine([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        sender.Send(new GetMyOrdersQuery(this.User.GetUserId(), page, pageSize), ct);

    /// <summary>Danh sách đơn theo trạng thái cho quầy (mặc định: các đơn đang chạy), mới nhất trước.</summary>
    /// <remarks>Ví dụ: GET /api/orders?status=Pending&amp;status=Preparing</remarks>
    [HttpGet]
    [Authorize(Policy = Policies.ProcessOrders)]
    [ProducesResponseType<PagedResult<OrderDto>>(StatusCodes.Status200OK)]
    public Task<PagedResult<OrderDto>> List(
        [FromQuery] OrderStatus[]? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default) =>
        sender.Send(new GetBaristaBoardQuery(status, page, pageSize), ct);

    /// <summary>Chi tiết 1 đơn — chỉ chủ đơn hoặc nhân viên xem được.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> GetById(int id, CancellationToken ct)
    {
        // 👉 Bước 11 (b47.md): IDOR — query trả null cả khi đơn KHÔNG CÓ lẫn khi KHÔNG PHẢI CỦA BẠN
        // → 404 (không phải 403) để không tiết lộ "đơn số 6 có tồn tại".
        OrderDto? order = await sender.Send(new GetOrderByIdQuery(id, this.User.ToCurrentUser()), ct);
        return order is null ? this.NotFound() : order;
    }

    /// <summary>Đổi trạng thái đơn (Barista/Admin) theo luồng Pending → Preparing → Ready → Completed.</summary>
    [HttpPut("{id:int}/status")]
    [Authorize(Policy = Policies.ProcessOrders)]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<OrderDto> ChangeStatus(int id, ChangeOrderStatusRequest request, CancellationToken ct) =>
        // request.Status!.Value: [Required] đã đảm bảo không null (thiếu → 400 trước khi vào action)
        sender.Send(new ChangeOrderStatusCommand(id, request.Status!.Value, this.User.ToCurrentUser()), ct);

    /// <summary>Hủy đơn: khách hủy đơn CỦA MÌNH khi còn Pending; nhân viên hủy được cả khi đang pha.</summary>
    [HttpPost("{id:int}/cancel")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<OrderDto> Cancel(int id, CancellationToken ct) =>
        sender.Send(new CancelOrderCommand(id, this.User.ToCurrentUser()), ct);
}
