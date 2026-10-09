// ============================================================================
// IOrderRepository.cs — cổng truy cập dữ liệu đơn hàng (Buổi 48 · Clean Architecture).
// Gom các truy vấn trước nằm rải trong OrdersController + OrderHub (Include, EF.Property "UserId"...).
// "Chủ đơn" (cột Orders.UserId) vẫn là SHADOW PROPERTY như b47: Domain.Order chưa biết khái niệm tài khoản
// → interface nhận/trả ownerUserId riêng, Infrastructure đọc/ghi shadow property. (b53 sẽ bàn lại chuyện này.)
// ============================================================================
using CyberCafe.Contracts.Common;
using CyberCafe.Domain.Orders;

namespace CyberCafe.Application.Orders;

// 👉 Bước 6 (b48.md)
/// <summary>Đọc/ghi đơn hàng. Cài đặt: Infrastructure/Persistence/Repositories/OrderRepository.cs.</summary>
public interface IOrderRepository
{
    /// <summary>Đơn kèm dòng + món + giảm giá + thanh toán, ĐANG ĐƯỢC THEO DÕI (để đổi trạng thái); null nếu không có.</summary>
    Task<Order?> FindWithDetailsAsync(int id, CancellationToken ct = default);

    /// <summary>Thêm đơn mới, gắn chủ đơn (lấy từ token, KHÔNG từ body).</summary>
    void Add(Order order, int? ownerUserId);

    /// <summary>Chủ của 1 đơn ĐANG ĐƯỢC THEO DÕI (đọc shadow property).</summary>
    int? GetOwnerId(Order order);

    /// <summary>Chủ của đơn theo Id (chỉ SELECT 1 cột); null nếu đơn không tồn tại hoặc chưa có chủ.</summary>
    Task<int?> GetOwnerIdAsync(int orderId, CancellationToken ct = default);

    /// <summary>Đơn của 1 tài khoản, mới nhất trước (không tracking).</summary>
    Task<PagedResult<Order>> GetPageByOwnerAsync(int? ownerUserId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Đơn theo trạng thái (màn hình quầy), mới nhất trước (không tracking).</summary>
    Task<PagedResult<Order>> GetPageByStatusAsync(IReadOnlyCollection<OrderStatus> statuses, int page, int pageSize, CancellationToken ct = default);
}
