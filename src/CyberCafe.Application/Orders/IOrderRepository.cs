// ============================================================================
// IOrderRepository.cs — cổng GHI của aggregate Order (Buổi 48 · 49 · 51).
// b48: 1 interface vừa đọc vừa ghi (FindWithDetails, GetPageByOwner, GetPageByStatus, shadow "UserId"...).
// b49 (DDD): "1 repository cho 1 AGGREGATE ROOT" — chỉ tải/thêm NGUYÊN aggregate để gọi method nghiệp vụ.
//   Chủ đơn giờ là Order.OwnerId (property thật) → hết method GetOwnerId/shadow property.
// b51 (CQRS): mọi truy vấn hiển thị (danh sách, chi tiết) chuyển sang IOrderReadStore (phía ĐỌC).
// ⚠️ Lỗi hay gặp: thêm GetByStatusAsync, GetTodayRevenue... vào repository "cho tiện" → repository phình thành
//    "God object". Đọc để hiển thị → read store; đọc để SỬA → repository.
// ============================================================================
using CyberCafe.Domain.Orders;

namespace CyberCafe.Application.Orders;

// 👉 Bước 4 (b49.md)
/// <summary>Tải / thêm aggregate Order. Cài đặt: Infrastructure/Persistence/Repositories/OrderRepository.cs.</summary>
public interface IOrderRepository
{
    /// <summary>Tải NGUYÊN aggregate (dòng + món + giảm giá + thanh toán), đang được theo dõi để sửa; null nếu không có.</summary>
    Task<Order?> GetAsync(int id, CancellationToken ct = default);

    /// <summary>Thêm đơn mới (INSERT khi IUnitOfWork.SaveChangesAsync).</summary>
    void Add(Order order);
}
