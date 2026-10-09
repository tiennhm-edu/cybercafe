// ============================================================================
// IProductRepository.cs — cổng truy cập dữ liệu món (Buổi 48 · Clean Architecture).
// ADR 0001 (b47) nói: "khi Application không được tham chiếu EF Core mới thật sự cần interface truy cập dữ liệu".
// Đó chính là bây giờ. Thiết kế theo NHU CẦU của use case (interface "hẹp"), không phải IRepository<T> chung chung:
//   - Đọc trả DTO đã projection (SELECT đúng cột) — giữ tối ưu của b40, không kéo cả entity về rồi mới map.
//   - Ghi trả entity Domain đang được theo dõi (tracked) để use case gọi method domain rồi IUnitOfWork lưu.
// ⚠️ Lỗi hay gặp: interface trả IQueryable<Product> "cho linh hoạt" → Application lại viết LINQ phụ thuộc
//    EF (Include, AsNoTracking, ToListAsync) → rò rỉ hạ tầng, mất ý nghĩa của tầng.
// ============================================================================
using CyberCafe.Contracts.Common;
using CyberCafe.Contracts.Products;
using CyberCafe.Domain.Products;

namespace CyberCafe.Application.Products;

// 👉 Bước 6 (b48.md)
/// <summary>Đọc/ghi thực đơn. Cài đặt: Infrastructure/Persistence/Repositories/ProductRepository.cs (EF Core).</summary>
public interface IProductRepository
{
    /// <summary>1 trang thực đơn theo bộ lọc (projection sang DTO, không tracking).</summary>
    Task<PagedResult<ProductDto>> GetPageAsync(ProductQuery query, CancellationToken ct = default);

    /// <summary>Chi tiết 1 món dạng DTO; null nếu không có.</summary>
    Task<ProductDto?> GetDtoAsync(int id, CancellationToken ct = default);

    /// <summary>Món dạng entity ĐANG ĐƯỢC THEO DÕI (để sửa/xóa); null nếu không có.</summary>
    Task<Product?> FindAsync(int id, CancellationToken ct = default);

    /// <summary>Nhiều món theo Id trong 1 câu SQL (WHERE Id IN ...), tracked — dùng khi đặt hàng.</summary>
    Task<IReadOnlyDictionary<int, Product>> GetByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default);

    /// <summary>Đánh dấu thêm mới (INSERT khi IUnitOfWork.SaveChangesAsync).</summary>
    void Add(Product product);

    /// <summary>Đánh dấu xóa (DELETE khi IUnitOfWork.SaveChangesAsync).</summary>
    void Remove(Product product);

    /// <summary>Món đã từng có trong đơn hàng nào chưa (đã bán thì không cho xóa).</summary>
    Task<bool> IsSoldAsync(int productId, CancellationToken ct = default);
}
