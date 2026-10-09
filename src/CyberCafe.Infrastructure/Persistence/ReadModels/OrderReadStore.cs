// ============================================================================
// OrderReadStore.cs — READ MODEL đơn hàng: projection + AsNoTracking (Buổi 53 · CQRS read side).
// Phía GHI (OrderRepository) tải nguyên aggregate: Order + OrderItems + Products ĐẦY ĐỦ cột + Discount + Payment,
// EF theo dõi từng object để biết cái gì đổi. Màn hình quầy barista KHÔNG sửa gì → làm vậy là lãng phí.
// Ở đây: SELECT đúng các cột màn hình cần (tên/emoji món, không cần Description, Price, BeanType...),
// không tracking, không dựng aggregate, không chạy invariant. Kết quả: ít cột, ít object, ít RAM.
// Phần "đa hình" (Discount.Display(), Payment.Display()) vẫn cần object domain nhỏ → chỉ chiếu 2 entity đó.
// Muốn nhanh hơn nữa: Dapper + SQL view / bảng read model riêng cập nhật bằng domain event (bài tập b53).
// ============================================================================
using System.Linq.Expressions;
using CyberCafe.Application.Common;
using CyberCafe.Application.Orders.Queries;
using CyberCafe.Contracts.Common;
using CyberCafe.Contracts.Orders;
using CyberCafe.Domain.Common;
using CyberCafe.Domain.Discounts;
using CyberCafe.Domain.Orders;
using CyberCafe.Domain.Payments;
using CyberCafe.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace CyberCafe.Infrastructure.Persistence.ReadModels;

// 👉 Bước 1 (b53.md)
/// <summary>Truy vấn đơn hàng cho màn hình (projection → DTO).</summary>
public class OrderReadStore(CyberCafeDbContext db) : IOrderReadStore
{
    // Expression (không phải Func): EF đọc cây biểu thức → dịch thành 1 câu SELECT có JOIN, chỉ các cột được nhắc tới.
    // "i.Product is Drink" → điều kiện trên cột discriminator ProductType IN ('coffee','tea') (HasSize là code C#, không dịch được).
    private static readonly Expression<Func<Order, OrderRow>> Projection = o => new OrderRow(
        o.Id,
        o.Status,
        o.CreatedAt,
        o.Customer.FullName,
        o.Customer.PhoneNumber,
        o.Customer.LoyaltyPoints,
        o.Note,
        o.Items
            .OrderBy(i => EF.Property<int>(i, "Id")) // giữ đúng thứ tự thêm món (khóa shadow IDENTITY)
            .Select(i => new OrderLineRow(i.Product.Id, i.Product.Name, i.Product.Emoji, i.Product is Drink, i.Size, i.Quantity, i.UnitPrice))
            .ToList(),
        o.Discount,
        o.Payment);

    /// <inheritdoc />
    public async Task<OrderDto?> GetByIdAsync(int id, CurrentUser user, CancellationToken ct = default)
    {
        int? userId = user.UserId;
        bool isStaff = user.IsStaff;

        // IDOR ngay trong WHERE: (staff) OR (UserId = @me). Thêm "OwnerId != null" vì SQL so sánh NULL = NULL
        // theo kiểu C# (EF bù null-semantics) → khách ẩn danh (null) sẽ "thấy" đơn cũ không chủ nếu thiếu điều kiện này.
        OrderRow? row = await db.Orders.AsNoTracking()
            .Where(o => o.Id == id && (isStaff || (o.OwnerId != null && o.OwnerId == userId)))
            .Select(Projection)
            .FirstOrDefaultAsync(ct);
        return row is null ? null : ToDto(row);
    }

    /// <inheritdoc />
    public Task<PagedResult<OrderDto>> GetByOwnerAsync(int? ownerId, int page, int pageSize, CancellationToken ct = default) =>
        PageAsync(db.Orders.AsNoTracking().Where(o => o.OwnerId == ownerId), page, pageSize, ct);

    // 👉 Bước 2 (b53.md): bảng quầy barista — WHERE Status IN (...) ORDER BY CreatedAt DESC OFFSET/FETCH, dùng index IX_Orders_Status
    /// <inheritdoc />
    public Task<PagedResult<OrderDto>> GetByStatusAsync(IReadOnlyCollection<OrderStatus> statuses, int page, int pageSize, CancellationToken ct = default) =>
        PageAsync(db.Orders.AsNoTracking().Where(o => statuses.Contains(o.Status)), page, pageSize, ct);

    private static async Task<PagedResult<OrderDto>> PageAsync(IQueryable<Order> query, int page, int pageSize, CancellationToken ct)
    {
        int total = await query.CountAsync(ct);
        List<OrderRow> rows = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(Projection)
            .ToListAsync(ct);
        return new PagedResult<OrderDto>(rows.Select(ToDto).ToList(), page, pageSize, total);
    }

    // Tính số liệu trong bộ nhớ — CÙNG công thức với aggregate (Order.TotalAmount/DiscountAmount/FinalAmount).
    // ⚠️ Lỗi hay gặp: phía đọc tự "sáng tạo" công thức khác phía ghi → màn hình quầy lệch số tiền khách đã trả.
    //    Test tích hợp ReadModel_MatchesWriteModel so 2 phía với nhau.
    private static OrderDto ToDto(OrderRow row)
    {
        List<OrderItemDto> items = row.Items
            .Select(i => new OrderItemDto(i.ProductId, i.ProductName, i.Emoji, i.HasSize, i.Size, i.Quantity,
                i.UnitPrice.Amount, (i.UnitPrice * i.Quantity).Amount))
            .ToList();
        decimal total = items.Sum(i => i.LineTotal);
        decimal discount = row.Discount?.GetDiscountAmount(total) ?? 0;

        return new OrderDto(row.Id, Order.FormatCode(row.Id), row.Status, row.CreatedAt, row.CustomerName, row.PhoneNumber,
            row.LoyaltyPoints, row.Note, items, total, row.Discount?.Display(), discount, total - discount,
            row.Payment?.Method, row.Payment?.Display());
    }

    // Hình dạng dữ liệu đọc từ SQL (private: chỉ là bước trung gian trước khi thành OrderDto)
    private sealed record OrderRow(
        int Id, OrderStatus Status, DateTime CreatedAt, string CustomerName, string PhoneNumber, int LoyaltyPoints,
        string? Note, List<OrderLineRow> Items, Discount? Discount, Payment? Payment);

    private sealed record OrderLineRow(int ProductId, string ProductName, string Emoji, bool HasSize, DrinkSize Size, int Quantity, Money UnitPrice);
}
