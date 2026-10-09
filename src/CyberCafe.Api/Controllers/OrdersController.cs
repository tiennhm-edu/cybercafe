// ============================================================================
// OrdersController.cs — đặt hàng & xử lý đơn /api/orders (Buổi 32–35 · REST;
//                       Buổi 33–34 · thông báo SignalR; Buổi 36–41 · Include, tracking).
//   POST /api/orders                 201 | 400 (dữ liệu sai) | 409 (món tạm hết)
//   GET  /api/orders?status=...      200 — danh sách cho quầy barista
//   GET  /api/orders/{id}            200 | 404
//   PUT  /api/orders/{id}/status     200 | 400 | 404 | 409 (bước chuyển không hợp lệ)
//   POST /api/orders/{id}/cancel     200 | 404 | 409
// ============================================================================
using CyberCafe.Api.Data;
using CyberCafe.Api.Mapping;
using CyberCafe.Api.Realtime;
using CyberCafe.Contracts.Common;
using CyberCafe.Contracts.Orders;
using CyberCafe.Domain.Discounts;
using CyberCafe.Domain.Orders;
using CyberCafe.Domain.Payments;
using CyberCafe.Domain.People;
using CyberCafe.Domain.Products;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CyberCafe.Api.Controllers;

/// <summary>Đặt hàng và xử lý đơn.</summary>
[ApiController]
[Route("api/orders")]
public class OrdersController(CyberCafeDbContext db, IOrderNotifier notifier) : ControllerBase
{
    /// <summary>Đặt hàng: server tự tra giá, mã giảm giá, tính tiền và tạo thanh toán.</summary>
    [HttpPost]
    [ProducesResponseType<OrderDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderDto>> Place(PlaceOrderRequest request, CancellationToken ct)
    {
        // 👉 Bước 7 (b40.md)
        // 1) Lấy các món trong 1 câu SQL: WHERE Id IN (@p0, @p1...) — không query từng món trong vòng lặp (N+1).
        //    Không AsNoTracking: Order sẽ tham chiếu tới các Product này; nếu Product không được theo dõi,
        //    EF tưởng là món MỚI và cố INSERT lại → lỗi trùng khóa.
        int[] ids = request.Items.Select(i => i.ProductId).Distinct().ToArray();
        Dictionary<int, Product> products = await db.Products
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        int[] missing = ids.Where(id => !products.ContainsKey(id)).ToArray();
        if (missing.Length > 0)
        {
            this.ModelState.AddModelError(nameof(request.Items), $"Không có món với mã: {string.Join(", ", missing)}");
            return this.ValidationProblem(this.ModelState);
        }

        // 2) Mã giảm giá: client chỉ gửi MÃ, server tự tra (DiscountCatalog dùng chung với Web).
        Discount? discount = null;
        if (!string.IsNullOrWhiteSpace(request.DiscountCode))
        {
            discount = DiscountCatalog.FindByCode(request.DiscountCode);
            if (discount is null)
            {
                this.ModelState.AddModelError(nameof(request.DiscountCode), "Mã giảm giá không hợp lệ");
                return this.ValidationProblem(this.ModelState);
            }
        }

        // 3) Dựng đơn bằng CHÍNH domain của buổi 24–31: Cart → ToOrder → Checkout(Payment).
        Order order;
        try
        {
            Cart cart = new();
            foreach (OrderLineRequest line in request.Items)
            {
                cart.AddItem(products[line.ProductId], line.Size, line.Quantity); // món tạm hết → InvalidOperationException
            }

            if (discount is not null)
            {
                cart.ApplyDiscount(discount);
            }

            order = cart.ToOrder(new Customer(request.CustomerName, request.PhoneNumber), request.Note);
            order.Checkout(PaymentFactory.Create(request.PaymentMethod, order.FinalAmount, request.PhoneNumber, request.CardNumber));
        }
        catch (ArgumentException ex)
        {
            this.ModelState.AddModelError(string.Empty, ex.Message);
            return this.ValidationProblem(this.ModelState);
        }
        catch (InvalidOperationException ex)
        {
            return this.Problem(statusCode: StatusCodes.Status409Conflict, title: "Không đặt được đơn", detail: ex.Message);
        }

        // 4) Lưu: EF INSERT Payments, OrderDiscounts, Orders, OrderItems trong 1 TRANSACTION
        //    (SaveChanges tự bọc transaction — lỗi giữa chừng thì không bảng nào bị ghi dở).
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);

        // 5) Thông báo realtime SAU khi lưu thành công (lưu lỗi thì không báo đơn "ma" cho barista).
        OrderDto dto = order.ToDto();
        await notifier.OrderPlacedAsync(dto, ct);

        return this.CreatedAtAction(nameof(this.GetById), new { id = order.Id }, dto);
    }

    /// <summary>Danh sách đơn theo trạng thái (mặc định: các đơn đang chạy), mới nhất trước.</summary>
    /// <remarks>Ví dụ: GET /api/orders?status=Pending&amp;status=Preparing</remarks>
    [HttpGet]
    [ProducesResponseType<PagedResult<OrderDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<OrderDto>>> List(
        [FromQuery] OrderStatus[]? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        // Kẹp tham số vào khoảng an toàn thay vì báo lỗi: màn hình barista luôn nhận được dữ liệu
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        OrderStatus[] wanted = status is { Length: > 0 }
            ? status
            : [OrderStatus.Pending, OrderStatus.Preparing, OrderStatus.Ready];

        // wanted.Contains(o.Status) → SQL: WHERE Status IN (N'Pending', N'Preparing', N'Ready')
        IQueryable<Order> query = this.OrdersWithDetails().AsNoTracking().Where(o => wanted.Contains(o.Status));
        int total = await query.CountAsync(ct);
        List<Order> orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<OrderDto>(orders.Select(o => o.ToDto()).ToList(), page, pageSize, total);
    }

    /// <summary>Chi tiết 1 đơn.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> GetById(int id, CancellationToken ct)
    {
        // Buổi 42–47: thêm kiểm tra "đơn này có phải của người đang gọi không" (chống IDOR)
        Order? order = await this.OrdersWithDetails().AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, ct);
        return order is null ? this.NotFound() : order.ToDto();
    }

    /// <summary>Đổi trạng thái đơn theo luồng Pending → Preparing → Ready → Completed (hoặc Cancelled).</summary>
    [HttpPut("{id:int}/status")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    // request.Status!.Value: [Required] đã đảm bảo không null (thiếu → 400 trước khi vào action)
    public Task<ActionResult<OrderDto>> ChangeStatus(int id, ChangeOrderStatusRequest request, CancellationToken ct) =>
        this.TransitionAsync(id, request.Status!.Value, ct);

    /// <summary>Hủy đơn (chỉ khi đơn còn Pending/Preparing).</summary>
    [HttpPost("{id:int}/cancel")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<OrderDto>> Cancel(int id, CancellationToken ct) =>
        this.TransitionAsync(id, OrderStatus.Cancelled, ct);

    // Dùng chung cho đổi trạng thái và hủy: tải đơn (tracked) → domain kiểm tra luật → lưu → thông báo.
    private async Task<ActionResult<OrderDto>> TransitionAsync(int id, OrderStatus next, CancellationToken ct)
    {
        Order? order = await this.OrdersWithDetails().FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null)
        {
            return this.NotFound();
        }

        try
        {
            order.ChangeStatus(next); // luật nằm ở domain (OrderStatusFlow)
        }
        catch (InvalidOperationException ex)
        {
            // 409 Conflict: request đúng định dạng nhưng XUNG ĐỘT với trạng thái hiện tại của tài nguyên.
            return this.Problem(statusCode: StatusCodes.Status409Conflict, title: "Không đổi được trạng thái", detail: ex.Message);
        }

        await db.SaveChangesAsync(ct); // chỉ UPDATE Orders SET Status = ... (change tracking)
        OrderDto dto = order.ToDto();
        await notifier.OrderStatusChangedAsync(dto, ct);
        return dto;
    }

    // 👉 Bước 7 (b40.md): EAGER LOADING. Mặc định EF KHÔNG tự tải bảng liên quan:
    // thiếu Include → order.Items rỗng, item.Product null. Include sinh LEFT JOIN trong 1 câu SQL.
    // ⚠️ Lỗi hay gặp: vòng lặp foreach (order) { db.Entry(order).Collection(...).Load() } → N+1 câu SQL.
    private IQueryable<Order> OrdersWithDetails() => db.Orders
        .Include(o => o.Items).ThenInclude(i => i.Product)
        .Include(o => o.Discount)
        .Include(o => o.Payment);
}
