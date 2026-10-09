// ============================================================================
// OrderService.cs — use case đặt hàng & xử lý đơn (Buổi 48 · Clean Architecture).
// Logic CHUYỂN NGUYÊN từ OrdersController (b47) + OrderHub.WatchOrder, không đổi hành vi:
//   PlaceAsync        — tra món, tra mã giảm giá, dựng đơn bằng Domain, lưu, báo barista.
//   GetMine / List    — phân trang đơn của khách / đơn theo trạng thái cho quầy.
//   GetByIdAsync      — chống IDOR: không phải chủ đơn (và không phải nhân viên) → coi như không tồn tại.
//   ChangeStatus / Cancel — luật domain (OrderStatusFlow) + luật riêng của khách (chỉ hủy khi Pending).
// Khác b47: không còn ControllerBase/ModelState/Problem(...) → lỗi là exception có nghĩa (AppExceptions.cs).
// ============================================================================
using CyberCafe.Application.Common;
using CyberCafe.Application.Common.Exceptions;
using CyberCafe.Application.Common.Interfaces;
using CyberCafe.Application.Products;
using CyberCafe.Contracts.Common;
using CyberCafe.Contracts.Orders;
using CyberCafe.Domain.Discounts;
using CyberCafe.Domain.Orders;
using CyberCafe.Domain.Payments;
using CyberCafe.Domain.People;
using CyberCafe.Domain.Products;

namespace CyberCafe.Application.Orders;

// 👉 Bước 8 (b48.md)
/// <summary>Đặt hàng và xử lý đơn.</summary>
public class OrderService(
    IProductRepository products,
    IOrderRepository orders,
    IUnitOfWork unitOfWork,
    IOrderNotifier notifier)
{
    // Đơn "đang chạy" mà quầy cần thấy khi không truyền ?status=
    private static readonly OrderStatus[] ActiveStatuses = [OrderStatus.Pending, OrderStatus.Preparing, OrderStatus.Ready];

    /// <summary>Đặt hàng: server tự tra giá, mã giảm giá, tính tiền và tạo thanh toán.</summary>
    /// <param name="request">Body đã qua DataAnnotations ở Api.</param>
    /// <param name="ownerUserId">Id khách lấy từ TOKEN (không bao giờ từ body).</param>
    /// <param name="ct">Hủy khi client ngắt kết nối.</param>
    /// <exception cref="ValidationException">Món không tồn tại / mã giảm giá sai (→ 400).</exception>
    public async Task<OrderDto> PlaceAsync(PlaceOrderRequest request, int? ownerUserId, CancellationToken ct = default)
    {
        // 1) Lấy các món trong 1 câu SQL (WHERE Id IN ...) — không query từng món (N+1). Repository trả bản TRACKED:
        //    Order sẽ tham chiếu các Product này; không tracked thì EF tưởng là món MỚI và INSERT lại → trùng khóa.
        int[] ids = request.Items.Select(i => i.ProductId).Distinct().ToArray();
        IReadOnlyDictionary<int, Product> found = await products.GetByIdsAsync(ids, ct);

        int[] missing = ids.Where(id => !found.ContainsKey(id)).ToArray();
        if (missing.Length > 0)
        {
            throw ValidationException.For(nameof(request.Items), $"Không có món với mã: {string.Join(", ", missing)}");
        }

        // 2) Mã giảm giá: client chỉ gửi MÃ, server tự tra (DiscountCatalog dùng chung với Web).
        Discount? discount = null;
        if (!string.IsNullOrWhiteSpace(request.DiscountCode))
        {
            discount = DiscountCatalog.FindByCode(request.DiscountCode)
                ?? throw ValidationException.For(nameof(request.DiscountCode), "Mã giảm giá không hợp lệ");
        }

        // 3) Dựng đơn bằng CHÍNH domain của buổi 24–31: Cart → ToOrder → Checkout(Payment).
        //    Món tạm hết (InvalidOperationException) → 409; dữ liệu sai (ArgumentException) → 400.
        Cart cart = new();
        foreach (OrderLineRequest line in request.Items)
        {
            cart.AddItem(found[line.ProductId], line.Size, line.Quantity);
        }

        if (discount is not null)
        {
            cart.ApplyDiscount(discount);
        }

        Order order = cart.ToOrder(new Customer(request.CustomerName, request.PhoneNumber), request.Note);
        order.Checkout(PaymentFactory.Create(request.PaymentMethod, order.FinalAmount, request.PhoneNumber, request.CardNumber));

        // 4) Lưu: INSERT Payments, OrderDiscounts, Orders, OrderItems trong 1 TRANSACTION (SaveChanges).
        orders.Add(order, ownerUserId);
        await unitOfWork.SaveChangesAsync(ct);

        // 5) Báo realtime SAU khi lưu thành công (lưu lỗi thì không báo đơn "ma" cho barista).
        OrderDto dto = order.ToDto();
        await notifier.OrderPlacedAsync(dto, ct);
        return dto;
    }

    /// <summary>Đơn của chính khách đang đăng nhập, mới nhất trước.</summary>
    public async Task<PagedResult<OrderDto>> GetMineAsync(int? userId, int page, int pageSize, CancellationToken ct = default)
    {
        (page, pageSize) = Clamp(page, pageSize);
        return ToDtoPage(await orders.GetPageByOwnerAsync(userId, page, pageSize, ct));
    }

    /// <summary>Đơn theo trạng thái cho quầy (mặc định: các đơn đang chạy), mới nhất trước.</summary>
    public async Task<PagedResult<OrderDto>> ListAsync(OrderStatus[]? statuses, int page, int pageSize, CancellationToken ct = default)
    {
        OrderStatus[] wanted = statuses is { Length: > 0 } ? statuses : ActiveStatuses;
        (page, pageSize) = Clamp(page, pageSize);
        return ToDtoPage(await orders.GetPageByStatusAsync(wanted, page, pageSize, ct));
    }

    /// <summary>Chi tiết 1 đơn; null nếu không có HOẶC người gọi không được xem (chống IDOR → 404).</summary>
    public async Task<OrderDto?> GetByIdAsync(int id, CurrentUser user, CancellationToken ct = default)
    {
        Order? order = await orders.FindWithDetailsAsync(id, ct);
        return order is not null && user.CanAccess(orders.GetOwnerId(order)) ? order.ToDto() : null;
    }

    /// <summary>Đổi trạng thái theo OrderStatusFlow (nhân viên).</summary>
    /// <exception cref="NotFoundException">Không có đơn / không được xem (→ 404).</exception>
    public Task<OrderDto> ChangeStatusAsync(int id, OrderStatus next, CurrentUser user, CancellationToken ct = default) =>
        this.TransitionAsync(id, next, user, ct);

    /// <summary>Hủy đơn: khách hủy đơn CỦA MÌNH khi còn Pending; nhân viên hủy được cả khi đang pha.</summary>
    /// <exception cref="NotFoundException">Không có đơn / không phải chủ đơn (→ 404).</exception>
    /// <exception cref="ConflictException">Khách hủy khi quán đã bắt đầu pha (→ 409).</exception>
    public Task<OrderDto> CancelAsync(int id, CurrentUser user, CancellationToken ct = default) =>
        this.TransitionAsync(id, OrderStatus.Cancelled, user, ct);

    /// <summary>Hub: được theo dõi realtime đơn này không (nhân viên, hoặc chủ đơn).</summary>
    public async Task<bool> CanWatchAsync(int orderId, CurrentUser user, CancellationToken ct = default) =>
        // Nhân viên khỏi truy vấn; khách thì chỉ SELECT 1 cột UserId (projection), không tải cả đơn
        user.IsStaff || user.CanAccess(await orders.GetOwnerIdAsync(orderId, ct));

    // Dùng chung cho đổi trạng thái và hủy: tải (tracked) → kiểm tra quyền → domain kiểm tra luật → lưu → báo.
    private async Task<OrderDto> TransitionAsync(int id, OrderStatus next, CurrentUser user, CancellationToken ct)
    {
        Order? order = await orders.FindWithDetailsAsync(id, ct);
        if (order is null || !user.CanAccess(orders.GetOwnerId(order)))
        {
            throw new NotFoundException($"Không có đơn {id}"); // đơn của người khác: giả như không tồn tại
        }

        // Luật riêng cho KHÁCH: chỉ hủy khi barista chưa bắt đầu pha (nhân viên thì theo OrderStatusFlow)
        if (!user.IsStaff && order.Status != OrderStatus.Pending)
        {
            throw new ConflictException("Quán đã bắt đầu pha chế, vui lòng liên hệ quầy để hủy.", "Không hủy được đơn");
        }

        // Luật domain (OrderStatusFlow). Sai luồng → InvalidOperationException → 409.
        order.ChangeStatus(next);

        await unitOfWork.SaveChangesAsync(ct); // chỉ UPDATE Orders SET Status = ... (change tracking)
        OrderDto dto = order.ToDto();
        await notifier.OrderStatusChangedAsync(dto, ct);
        return dto;
    }

    // Kẹp tham số vào khoảng an toàn thay vì báo lỗi: màn hình luôn nhận được dữ liệu
    private static (int Page, int PageSize) Clamp(int page, int pageSize) => (Math.Max(1, page), Math.Clamp(pageSize, 1, 100));

    private static PagedResult<OrderDto> ToDtoPage(PagedResult<Order> page) =>
        new(page.Items.Select(o => o.ToDto()).ToList(), page.Page, page.PageSize, page.TotalCount);
}
