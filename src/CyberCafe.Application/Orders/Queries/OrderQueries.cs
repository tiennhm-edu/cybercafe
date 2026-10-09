// ============================================================================
// OrderQueries.cs — các QUERY đọc đơn hàng (Buổi 51 · query; Buổi 53 · read model).
//   GetOrderByIdQuery     GET /api/orders/{id}   + hub WatchOrder (chủ đơn / nhân viên)
//   GetMyOrdersQuery      GET /api/orders/mine   (khách)
//   GetBaristaBoardQuery  GET /api/orders?status (quầy barista)
// Query handler KHÔNG dùng IOrderRepository/aggregate: không sửa gì nên không cần invariant, không cần tracking.
// Chúng đọc qua IOrderReadStore (projection thẳng ra DTO). Đây là chữ "S" (Segregation) trong CQRS:
// đường GHI và đường ĐỌC tách nhau, mỗi đường tối ưu theo cách riêng.
// ============================================================================
using CyberCafe.Application.Common;
using CyberCafe.Application.Common.Messaging;
using CyberCafe.Contracts.Common;
using CyberCafe.Contracts.Orders;
using CyberCafe.Domain.Orders;

namespace CyberCafe.Application.Orders.Queries;

// 👉 Bước 6 (b51.md)
/// <summary>Chi tiết 1 đơn; null nếu không có HOẶC người gọi không được xem (→ 404, chống IDOR).</summary>
/// <param name="OrderId">Id đơn.</param>
/// <param name="User">Người gọi.</param>
public sealed record GetOrderByIdQuery(int OrderId, CurrentUser User) : IQuery<OrderDto?>;

/// <summary>Đơn của chính khách đang đăng nhập, mới nhất trước.</summary>
/// <param name="UserId">Id khách (từ token).</param>
/// <param name="Page">Trang (kẹp về ≥ 1).</param>
/// <param name="PageSize">Cỡ trang (kẹp về 1–100).</param>
public sealed record GetMyOrdersQuery(int? UserId, int Page = 1, int PageSize = 20) : IQuery<PagedResult<OrderDto>>;

// 👉 Bước 2 (b53.md)
/// <summary>Bảng của quầy: đơn theo trạng thái (mặc định các đơn đang chạy), mới nhất trước.</summary>
/// <param name="Statuses">Trạng thái cần xem (rỗng = Pending/Preparing/Ready).</param>
/// <param name="Page">Trang.</param>
/// <param name="PageSize">Cỡ trang.</param>
public sealed record GetBaristaBoardQuery(IReadOnlyCollection<OrderStatus>? Statuses, int Page = 1, int PageSize = 50) : IQuery<PagedResult<OrderDto>>;

/// <summary>Handler của 3 query đơn hàng (cùng dùng IOrderReadStore nên gom 1 class).</summary>
public sealed class OrderQueryHandlers(IOrderReadStore readStore) :
    IQueryHandler<GetOrderByIdQuery, OrderDto?>,
    IQueryHandler<GetMyOrdersQuery, PagedResult<OrderDto>>,
    IQueryHandler<GetBaristaBoardQuery, PagedResult<OrderDto>>
{
    // Đơn "đang chạy" mà quầy cần thấy khi không truyền ?status=
    private static readonly OrderStatus[] ActiveStatuses = [OrderStatus.Pending, OrderStatus.Preparing, OrderStatus.Ready];

    /// <inheritdoc />
    public Task<OrderDto?> Handle(GetOrderByIdQuery query, CancellationToken ct) =>
        readStore.GetByIdAsync(query.OrderId, query.User, ct);

    /// <inheritdoc />
    public Task<PagedResult<OrderDto>> Handle(GetMyOrdersQuery query, CancellationToken ct)
    {
        (int page, int pageSize) = Clamp(query.Page, query.PageSize);
        return readStore.GetByOwnerAsync(query.UserId, page, pageSize, ct);
    }

    /// <inheritdoc />
    public Task<PagedResult<OrderDto>> Handle(GetBaristaBoardQuery query, CancellationToken ct)
    {
        IReadOnlyCollection<OrderStatus> wanted = query.Statuses is { Count: > 0 } ? query.Statuses : ActiveStatuses;
        (int page, int pageSize) = Clamp(query.Page, query.PageSize);
        return readStore.GetByStatusAsync(wanted, page, pageSize, ct);
    }

    // Kẹp tham số vào khoảng an toàn thay vì báo lỗi: màn hình luôn nhận được dữ liệu (giữ hành vi b40–b48)
    private static (int Page, int PageSize) Clamp(int page, int pageSize) => (Math.Max(1, page), Math.Clamp(pageSize, 1, 100));
}
