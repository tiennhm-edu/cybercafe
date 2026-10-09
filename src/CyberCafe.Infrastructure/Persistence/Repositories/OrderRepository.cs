// ============================================================================
// OrderRepository.cs — cài đặt IOrderRepository bằng EF Core (Buổi 36–41 · 42–47 · 48).
// Gom các truy vấn trước nằm trong OrdersController + OrderHub (b47):
//   - Include/ThenInclude (eager loading) cho đơn đầy đủ.
//   - Shadow property "UserId" (chủ đơn): ghi qua db.Entry(...).Property(...), lọc bằng EF.Property<int?>(...).
// Application chỉ thấy "ownerUserId" kiểu int? — không biết khái niệm shadow property của EF.
// ============================================================================
using CyberCafe.Application.Orders;
using CyberCafe.Contracts.Common;
using CyberCafe.Domain.Orders;
using CyberCafe.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace CyberCafe.Infrastructure.Persistence.Repositories;

// 👉 Bước 6 (b48.md)
/// <summary>Truy cập bảng Orders (+ OrderItems, OrderDiscounts, Payments).</summary>
public class OrderRepository(CyberCafeDbContext db) : IOrderRepository
{
    /// <inheritdoc />
    public Task<Order?> FindWithDetailsAsync(int id, CancellationToken ct = default) =>
        // Không AsNoTracking: GetOwnerId đọc shadow property qua db.Entry(order) và use case còn đổi trạng thái
        this.OrdersWithDetails().FirstOrDefaultAsync(o => o.Id == id, ct);

    /// <inheritdoc />
    public void Add(Order order, int? ownerUserId)
    {
        db.Orders.Add(order);
        // 👉 Bước 11 (b47.md): gắn CHỦ ĐƠN = người trong token. Ghi vào shadow property "UserId"
        // qua Entry(...).Property(...): domain Order không cần biết User.
        db.Entry(order).Property(OrderConfiguration.OwnerUserId).CurrentValue = ownerUserId;
    }

    // Giá trị shadow property đọc qua db.Entry(order) → entity phải đang được EF theo dõi (tracked).
    /// <inheritdoc />
    public int? GetOwnerId(Order order) =>
        db.Entry(order).Property<int?>(OrderConfiguration.OwnerUserId).CurrentValue;

    /// <inheritdoc />
    public Task<int?> GetOwnerIdAsync(int orderId, CancellationToken ct = default) => db.Orders
        .Where(o => o.Id == orderId)
        .Select(o => EF.Property<int?>(o, OrderConfiguration.OwnerUserId)) // chỉ SELECT 1 cột
        .FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    public Task<PagedResult<Order>> GetPageByOwnerAsync(int? ownerUserId, int page, int pageSize, CancellationToken ct = default) =>
        // EF.Property<int?>(o, "UserId"): lọc theo shadow property → WHERE o.UserId = @userId
        PageAsync(this.OrdersWithDetails().AsNoTracking()
            .Where(o => EF.Property<int?>(o, OrderConfiguration.OwnerUserId) == ownerUserId), page, pageSize, ct);

    /// <inheritdoc />
    public Task<PagedResult<Order>> GetPageByStatusAsync(IReadOnlyCollection<OrderStatus> statuses, int page, int pageSize, CancellationToken ct = default) =>
        // statuses.Contains(o.Status) → SQL: WHERE Status IN (N'Pending', N'Preparing', N'Ready')
        PageAsync(this.OrdersWithDetails().AsNoTracking().Where(o => statuses.Contains(o.Status)), page, pageSize, ct);

    private static async Task<PagedResult<Order>> PageAsync(IQueryable<Order> query, int page, int pageSize, CancellationToken ct)
    {
        int total = await query.CountAsync(ct);
        List<Order> orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return new PagedResult<Order>(orders, page, pageSize, total);
    }

    // 👉 Bước 7 (b40.md): EAGER LOADING. Mặc định EF KHÔNG tự tải bảng liên quan:
    // thiếu Include → order.Items rỗng, item.Product null. Include sinh LEFT JOIN trong 1 câu SQL.
    // ⚠️ Lỗi hay gặp: vòng lặp foreach (order) { db.Entry(order).Collection(...).Load() } → N+1 câu SQL.
    private IQueryable<Order> OrdersWithDetails() => db.Orders
        .Include(o => o.Items).ThenInclude(i => i.Product)
        .Include(o => o.Discount)
        .Include(o => o.Payment);
}
