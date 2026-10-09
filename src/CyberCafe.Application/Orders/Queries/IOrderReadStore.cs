// ============================================================================
// IOrderReadStore.cs — cổng ĐỌC đơn hàng cho màn hình (Buổi 51 · 53 · CQRS read side).
// Phía đọc KHÔNG cần aggregate: không sửa gì, không cần invariant → trả thẳng DTO đã chiếu (projection),
// AsNoTracking, chỉ SELECT cột cần. Đó là "read model": tối ưu cho MÀN HÌNH, không phải cho nghiệp vụ.
// Cài đặt hiện tại (Infrastructure/Persistence/ReadModels/OrderReadStore.cs) dùng chung database với phía ghi.
// Sau này có thể đổi sang Dapper/SQL view/bảng riêng/Redis... mà không đụng tới aggregate Order.
// ============================================================================
using CyberCafe.Application.Common;
using CyberCafe.Contracts.Common;
using CyberCafe.Contracts.Orders;
using CyberCafe.Domain.Orders;

namespace CyberCafe.Application.Orders.Queries;

// 👉 Bước 1 (b53.md)
/// <summary>Truy vấn đơn hàng trả DTO (không tracking, không tải aggregate).</summary>
public interface IOrderReadStore
{
    /// <summary>
    /// Chi tiết 1 đơn — luật IDOR nằm NGAY trong câu truy vấn (WHERE Id = @id AND (staff OR UserId = @me)):
    /// không phải của bạn thì kết quả rỗng y như không tồn tại.
    /// </summary>
    Task<OrderDto?> GetByIdAsync(int id, CurrentUser user, CancellationToken ct = default);

    /// <summary>Đơn của 1 tài khoản, mới nhất trước.</summary>
    Task<PagedResult<OrderDto>> GetByOwnerAsync(int? ownerId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Đơn theo trạng thái (bảng của quầy barista), mới nhất trước.</summary>
    Task<PagedResult<OrderDto>> GetByStatusAsync(IReadOnlyCollection<OrderStatus> statuses, int page, int pageSize, CancellationToken ct = default);
}
