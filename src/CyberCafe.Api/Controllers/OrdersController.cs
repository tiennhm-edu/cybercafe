// ============================================================================
// OrdersController.cs — đặt hàng & xử lý đơn /api/orders (Buổi 32–35 · REST;
//                       Buổi 33–34 · thông báo SignalR; Buổi 36–41 · Include, tracking;
//                       Buổi 42–47 · phân quyền theo vai trò + chống IDOR).
//   POST /api/orders                 Customer        201 | 400 | 409 (món tạm hết)
//   GET  /api/orders/mine            Customer        200 — CHỈ đơn của mình
//   GET  /api/orders?status=...      Barista/Admin   200 — mọi đơn (màn hình quầy)
//   GET  /api/orders/{id}            chủ đơn / staff 200 | 404
//   PUT  /api/orders/{id}/status     Barista/Admin   200 | 400 | 404 | 409
//   POST /api/orders/{id}/cancel     chủ đơn (khi Pending) / staff   200 | 404 | 409
// Thiếu token → 401; có token nhưng sai vai trò → 403.
// ============================================================================
using CyberCafe.Api.Auth;
using CyberCafe.Api.Data;
using CyberCafe.Api.Data.Configurations;
using CyberCafe.Api.Mapping;
using CyberCafe.Api.Realtime;
using CyberCafe.Contracts.Common;
using CyberCafe.Contracts.Orders;
using CyberCafe.Domain.Discounts;
using CyberCafe.Domain.Orders;
using CyberCafe.Domain.Payments;
using CyberCafe.Domain.People;
using CyberCafe.Domain.Products;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CyberCafe.Api.Controllers;

/// <summary>Đặt hàng và xử lý đơn.</summary>
[ApiController]
[Route("api/orders")]
[Authorize] // mọi endpoint đơn hàng đều cần đăng nhập; từng action siết thêm bằng policy
public class OrdersController(CyberCafeDbContext db, IOrderNotifier notifier) : ControllerBase
{
    /// <summary>Đặt hàng (Customer): server tự tra giá, mã giảm giá, tính tiền và tạo thanh toán.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.PlaceOrders)]
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
        //    Buổi 42–47: bỏ try/catch — món tạm hết (InvalidOperationException) → 409, dữ liệu sai
        //    (ArgumentException) → 400 do DomainExceptionHandler xử lý chung cho mọi controller.
        Cart cart = new();
        foreach (OrderLineRequest line in request.Items)
        {
            cart.AddItem(products[line.ProductId], line.Size, line.Quantity);
        }

        if (discount is not null)
        {
            cart.ApplyDiscount(discount);
        }

        Order order = cart.ToOrder(new Customer(request.CustomerName, request.PhoneNumber), request.Note);
        order.Checkout(PaymentFactory.Create(request.PaymentMethod, order.FinalAmount, request.PhoneNumber, request.CardNumber));

        // 4) Lưu: EF INSERT Payments, OrderDiscounts, Orders, OrderItems trong 1 TRANSACTION.
        db.Orders.Add(order);
        // 👉 Bước 11 (b47.md): gắn CHỦ ĐƠN = người trong token (KHÔNG lấy từ body — client gửi gì cũng được).
        // Ghi vào shadow property "UserId" qua Entry(...).Property(...): domain Order không cần biết User.
        db.Entry(order).Property(OrderConfiguration.OwnerUserId).CurrentValue = this.User.GetUserId();
        await db.SaveChangesAsync(ct);

        // 5) Thông báo realtime SAU khi lưu thành công (lưu lỗi thì không báo đơn "ma" cho barista).
        OrderDto dto = order.ToDto();
        await notifier.OrderPlacedAsync(dto, ct);

        return this.CreatedAtAction(nameof(this.GetById), new { id = order.Id }, dto);
    }

    /// <summary>Đơn của chính khách đang đăng nhập, mới nhất trước.</summary>
    [HttpGet("mine")]
    [Authorize(Policy = Policies.PlaceOrders)]
    [ProducesResponseType<PagedResult<OrderDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<OrderDto>>> Mine([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        int? userId = this.User.GetUserId();
        // EF.Property<int?>(o, "UserId"): lọc theo shadow property → WHERE o.UserId = @userId
        IQueryable<Order> query = this.OrdersWithDetails().AsNoTracking()
            .Where(o => EF.Property<int?>(o, OrderConfiguration.OwnerUserId) == userId);
        return await PageAsync(query, page, pageSize, ct);
    }

    /// <summary>Danh sách đơn theo trạng thái cho quầy (mặc định: các đơn đang chạy), mới nhất trước.</summary>
    /// <remarks>Ví dụ: GET /api/orders?status=Pending&amp;status=Preparing</remarks>
    [HttpGet]
    [Authorize(Policy = Policies.ProcessOrders)]
    [ProducesResponseType<PagedResult<OrderDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<OrderDto>>> List(
        [FromQuery] OrderStatus[]? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        OrderStatus[] wanted = status is { Length: > 0 }
            ? status
            : [OrderStatus.Pending, OrderStatus.Preparing, OrderStatus.Ready];

        // wanted.Contains(o.Status) → SQL: WHERE Status IN (N'Pending', N'Preparing', N'Ready')
        IQueryable<Order> query = this.OrdersWithDetails().AsNoTracking().Where(o => wanted.Contains(o.Status));
        return await PageAsync(query, page, pageSize, ct);
    }

    /// <summary>Chi tiết 1 đơn — chỉ chủ đơn hoặc nhân viên xem được.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> GetById(int id, CancellationToken ct)
    {
        // Không AsNoTracking ở đây: CanAccess đọc shadow property UserId qua db.Entry(order) (cần entity được theo dõi)
        Order? order = await this.OrdersWithDetails().FirstOrDefaultAsync(o => o.Id == id, ct);

        // 👉 Bước 11 (b47.md): IDOR (Insecure Direct Object Reference) — khách A đổi URL /orders/5 thành /orders/6
        // để xem đơn (tên, SĐT) của khách B. Có đăng nhập KHÔNG có nghĩa là được xem MỌI đơn.
        // Trả 404 (không phải 403) để không tiết lộ "đơn số 6 có tồn tại".
        if (order is null || !this.CanAccess(order))
        {
            return this.NotFound();
        }

        return order.ToDto();
    }

    /// <summary>Đổi trạng thái đơn (Barista/Admin) theo luồng Pending → Preparing → Ready → Completed.</summary>
    [HttpPut("{id:int}/status")]
    [Authorize(Policy = Policies.ProcessOrders)]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<OrderDto>> ChangeStatus(int id, ChangeOrderStatusRequest request, CancellationToken ct) =>
        // request.Status!.Value: [Required] đã đảm bảo không null (thiếu → 400 trước khi vào action)
        this.TransitionAsync(id, request.Status!.Value, ct);

    /// <summary>Hủy đơn: khách hủy đơn CỦA MÌNH khi còn Pending; nhân viên hủy được cả khi đang pha.</summary>
    [HttpPost("{id:int}/cancel")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<OrderDto>> Cancel(int id, CancellationToken ct) =>
        this.TransitionAsync(id, OrderStatus.Cancelled, ct);

    // Dùng chung cho đổi trạng thái và hủy: tải đơn (tracked) → kiểm tra quyền → domain kiểm tra luật → lưu → thông báo.
    private async Task<ActionResult<OrderDto>> TransitionAsync(int id, OrderStatus next, CancellationToken ct)
    {
        Order? order = await this.OrdersWithDetails().FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null || !this.CanAccess(order))
        {
            return this.NotFound(); // đơn của người khác: giả như không tồn tại (chống IDOR)
        }

        // Luật riêng cho KHÁCH: chỉ hủy khi barista chưa bắt đầu pha (nhân viên thì theo OrderStatusFlow)
        if (!this.User.IsStaff() && order.Status != OrderStatus.Pending)
        {
            return this.Problem(statusCode: StatusCodes.Status409Conflict, title: "Không hủy được đơn",
                detail: "Quán đã bắt đầu pha chế, vui lòng liên hệ quầy để hủy.");
        }

        // Luật domain (OrderStatusFlow). Sai luồng → InvalidOperationException → 409 (DomainExceptionHandler).
        order.ChangeStatus(next);

        await db.SaveChangesAsync(ct); // chỉ UPDATE Orders SET Status = ... (change tracking)
        OrderDto dto = order.ToDto();
        await notifier.OrderStatusChangedAsync(dto, ct);
        return dto;
    }

    // Nhân viên xem được mọi đơn; khách chỉ đơn có UserId = mình.
    // Giá trị shadow property đọc qua db.Entry(order) → entity phải đang được EF theo dõi (tracked).
    private bool CanAccess(Order order)
    {
        if (this.User.IsStaff())
        {
            return true;
        }

        int? owner = db.Entry(order).Property<int?>(OrderConfiguration.OwnerUserId).CurrentValue;
        return owner is not null && owner == this.User.GetUserId();
    }

    private static async Task<PagedResult<OrderDto>> PageAsync(IQueryable<Order> query, int page, int pageSize, CancellationToken ct)
    {
        // Kẹp tham số vào khoảng an toàn thay vì báo lỗi: màn hình luôn nhận được dữ liệu
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        int total = await query.CountAsync(ct);
        List<Order> orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<OrderDto>(orders.Select(o => o.ToDto()).ToList(), page, pageSize, total);
    }

    // 👉 Bước 7 (b40.md): EAGER LOADING. Mặc định EF KHÔNG tự tải bảng liên quan:
    // thiếu Include → order.Items rỗng, item.Product null. Include sinh LEFT JOIN trong 1 câu SQL.
    // ⚠️ Lỗi hay gặp: vòng lặp foreach (order) { db.Entry(order).Collection(...).Load() } → N+1 câu SQL.
    private IQueryable<Order> OrdersWithDetails() => db.Orders
        .Include(o => o.Items).ThenInclude(i => i.Product)
        .Include(o => o.Discount)
        .Include(o => o.Payment);
}
