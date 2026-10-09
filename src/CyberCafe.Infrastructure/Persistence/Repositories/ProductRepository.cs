// ============================================================================
// ProductRepository.cs — cài đặt IProductRepository bằng EF Core (Buổi 36–41 · 48).
// Các câu LINQ ở đây CHUYỂN NGUYÊN từ ProductsController (b47): lọc/tìm/sắp xếp/phân trang, projection,
// truy vấn shadow property "ProductId". Khác biệt duy nhất: giờ nằm sau 1 interface của Application.
// Đây là "repository hẹp theo use case" (ADR 0002), KHÔNG phải IRepository<T> chung chung của lab b45.
// ============================================================================
using CyberCafe.Application.Products;
using CyberCafe.Contracts.Common;
using CyberCafe.Contracts.Products;
using CyberCafe.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace CyberCafe.Infrastructure.Persistence.Repositories;

// 👉 Bước 6 (b48.md)
/// <summary>Truy cập bảng Products (và kiểm tra OrderItems).</summary>
public class ProductRepository(CyberCafeDbContext db) : IProductRepository
{
    /// <inheritdoc />
    public async Task<PagedResult<ProductDto>> GetPageAsync(ProductQuery query, CancellationToken ct = default)
    {
        // 👉 Bước 6 (b40.md): xây IQueryable từng bước — CHƯA chạy SQL cho tới ToListAsync/CountAsync.
        // AsNoTracking: chỉ đọc để trả JSON → EF không cần "theo dõi" thay đổi → nhanh hơn, ít RAM hơn.
        IQueryable<Product> source = db.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string keyword = query.Search.Trim();
            // SQL Server: LIKE N'%keyword%' với collation không phân biệt hoa/thường.
            // ⚠️ EF InMemory (test) thì PHÂN BIỆT hoa/thường — test nhớ gõ đúng chữ hoa.
            source = source.Where(p => p.Name.Contains(keyword));
        }

        // "p is Coffee" → WHERE ProductType = N'coffee' (lọc trên cột discriminator)
        source = query.Type switch
        {
            ProductTypes.Coffee => source.Where(p => p is Coffee),
            ProductTypes.Tea => source.Where(p => p is Tea),
            ProductTypes.Cake => source.Where(p => p is Cake),
            _ => source
        };

        // bool? : null = không lọc; true/false = lọc theo cột IsAvailable
        if (query.Available is not null)
        {
            source = source.Where(p => p.IsAvailable == query.Available.Value);
        }

        // Đếm TRƯỚC khi Skip/Take: TotalCount là tổng sau lọc, không phải số món của 1 trang.
        int total = await source.CountAsync(ct);

        // Sắp xếp phải có trước Skip/Take — không có ORDER BY thì "trang 2" mỗi lần chạy có thể khác nhau.
        source = (query.SortBy, query.Desc) switch
        {
            ("name", false) => source.OrderBy(p => p.Name),
            ("name", true) => source.OrderByDescending(p => p.Name),
            ("price", false) => source.OrderBy(p => p.Price).ThenBy(p => p.Id),
            ("price", true) => source.OrderByDescending(p => p.Price).ThenBy(p => p.Id),
            (_, true) => source.OrderByDescending(p => p.Id),
            _ => source.OrderBy(p => p.Id)
        };

        List<ProductDto> items = await source
            .Skip((query.Page - 1) * query.PageSize)   // OFFSET
            .Take(query.PageSize)                       // FETCH NEXT
            .Select(ProductMapping.ToDtoExpression)     // projection: Expression nằm ở Application, EF dịch sang SQL
            .ToListAsync(ct);

        return new PagedResult<ProductDto>(items, query.Page, query.PageSize, total);
    }

    /// <inheritdoc />
    public Task<ProductDto?> GetDtoAsync(int id, CancellationToken ct = default) => db.Products
        .AsNoTracking()
        .Where(p => p.Id == id)
        .Select(ProductMapping.ToDtoExpression)
        .FirstOrDefaultAsync(ct);

    // KHÔNG AsNoTracking: cần EF theo dõi object để biết cột nào đổi khi SaveChanges
    /// <inheritdoc />
    public Task<Product?> FindAsync(int id, CancellationToken ct = default) =>
        db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<int, Product>> GetByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default) =>
        // ids.Contains(p.Id) → SQL: WHERE Id IN (@p0, @p1...) — 1 câu cho cả đơn
        await db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);

    /// <inheritdoc />
    public void Add(Product product) => db.Products.Add(product); // chỉ đánh dấu Added; SaveChanges mới gửi SQL

    /// <inheritdoc />
    public void Remove(Product product) => db.Products.Remove(product);

    // EF.Property<int>(i, "ProductId"): truy vấn trên SHADOW PROPERTY (cột khóa ngoại không có trong class)
    /// <inheritdoc />
    public Task<bool> IsSoldAsync(int productId, CancellationToken ct = default) =>
        db.OrderItems.AnyAsync(i => EF.Property<int>(i, "ProductId") == productId, ct);
}
