// ============================================================================
// OrderRepository.cs — cài đặt IOrderRepository bằng EF Core (Buổi 36–41 · 48 · 49).
// b48: gom truy vấn từ OrdersController + OrderHub (Include, shadow property "UserId", phân trang...).
// b49 (DDD): "repository theo aggregate" — chỉ còn tải NGUYÊN aggregate để sửa và thêm aggregate mới.
//   Danh sách / chi tiết để hiển thị đã chuyển sang ReadModels/OrderReadStore.cs (CQRS, b51–53).
// ============================================================================
using CyberCafe.Application.Orders;
using CyberCafe.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace CyberCafe.Infrastructure.Persistence.Repositories;

// 👉 Bước 4 (b49.md)
/// <summary>Tải / thêm aggregate Order (+ OrderItems, OrderDiscounts, Payments trong cùng cụm).</summary>
public class OrderRepository(CyberCafeDbContext db) : IOrderRepository
{
    // 👉 Bước 7 (b40.md): EAGER LOADING — aggregate phải được tải TRỌN cụm (dòng + món + giảm giá + thanh toán):
    // thiếu Payment thì Order.IsPaid sai → invariant "chưa trả tiền thì chưa pha" chặn nhầm.
    // ⚠️ Lỗi hay gặp: tải aggregate thiếu 1 phần rồi gọi method nghiệp vụ → luật chạy trên dữ liệu không đầy đủ.
    /// <inheritdoc />
    public Task<Order?> GetAsync(int id, CancellationToken ct = default) => db.Orders
        .Include(o => o.Items).ThenInclude(i => i.Product)
        .Include(o => o.Discount)
        .Include(o => o.Payment)
        .FirstOrDefaultAsync(o => o.Id == id, ct); // tracked: SaveChanges biết cột nào đổi + lấy được domain event

    /// <inheritdoc />
    public void Add(Order order) => db.Orders.Add(order); // OwnerId đã nằm trong aggregate (b49), không còn shadow property
}
