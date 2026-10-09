// ============================================================================
// MenuService.cs — use case quản lý thực đơn (Buổi 48 · Clean Architecture).
// Trước (b47) toàn bộ logic này nằm trong ProductsController: LINQ trên DbContext, cache, kiểm tra "đã bán".
// Bây giờ controller chỉ còn: nhận HTTP → gọi MenuService → trả HTTP. MenuService thì:
//   - Không biết HTTP (không IActionResult, không StatusCodes) → lỗi = exception có nghĩa (AppExceptions.cs).
//   - Không biết EF Core / Redis → chỉ dùng port: IProductRepository, IUnitOfWork, IMenuCache.
//   → Unit test được bằng bản giả (fake) của 3 interface, không cần database.
// Hành vi GIỮ NGUYÊN 100% so với b47 (test tích hợp cũ chạy lại không sửa dòng nào ngoài using).
// ============================================================================
using CyberCafe.Application.Caching;
using CyberCafe.Application.Common.Exceptions;
using CyberCafe.Application.Common.Interfaces;
using CyberCafe.Contracts.Common;
using CyberCafe.Contracts.Products;
using CyberCafe.Domain.Products;

namespace CyberCafe.Application.Products;

// 👉 Bước 7 (b48.md)
/// <summary>Đọc (qua cache) và ghi thực đơn.</summary>
public class MenuService(IProductRepository products, IUnitOfWork unitOfWork, IMenuCache menuCache)
{
    /// <summary>1 trang thực đơn — cache-aside, key = query string đã chuẩn hóa.</summary>
    public Task<CacheResult<PagedResult<ProductDto>>> GetPageAsync(ProductQuery query, CancellationToken ct = default) =>
        // ?page=1&type=tea và ?type=tea&page=1 dùng CHUNG 1 key nhờ ToQueryString() có thứ tự cố định
        menuCache.GetOrCreateAsync($"products?{query.ToQueryString()}", token => products.GetPageAsync(query, token), ct);

    /// <summary>Chi tiết 1 món (Value = null nếu không có — null không được cache).</summary>
    public Task<CacheResult<ProductDto?>> GetByIdAsync(int id, CancellationToken ct = default) =>
        menuCache.GetOrCreateAsync($"product:{id}", token => products.GetDtoAsync(id, token), ct);

    /// <summary>Thêm món. Domain validate tên/giá/loại → ArgumentException (→ 400).</summary>
    public async Task<ProductDto> CreateAsync(ProductRequest request, CancellationToken ct = default)
    {
        Product product = request.ToDomain();
        products.Add(product);
        await unitOfWork.SaveChangesAsync(ct); // INSERT ...; SELECT SCOPE_IDENTITY() → product.Id có giá trị
        return product.ToDto();
    }

    /// <summary>Sửa món (không đổi được loại).</summary>
    /// <exception cref="NotFoundException">Không có món (→ 404).</exception>
    /// <exception cref="ValidationException">Đổi loại món (→ 400).</exception>
    public async Task<ProductDto> UpdateAsync(int id, ProductRequest request, CancellationToken ct = default)
    {
        // Entity tracked: EF biết cột nào đổi → chỉ UPDATE các cột đó
        Product product = await products.FindAsync(id, ct) ?? throw new NotFoundException($"Không có món {id}");

        // TPH: đổi Coffee → Cake là đổi CLASS của object — EF không hỗ trợ "đổi kiểu" tại chỗ.
        if (ProductMapping.TypeOf(product) != request.Type)
        {
            throw ValidationException.For(nameof(request.Type), "Không đổi được loại món. Hãy tạo món mới.");
        }

        request.ApplyTo(product); // ArgumentException từ domain → 400
        await unitOfWork.SaveChangesAsync(ct);
        return product.ToDto();
    }

    /// <summary>Xóa món chưa từng bán.</summary>
    /// <exception cref="NotFoundException">Không có món (→ 404).</exception>
    /// <exception cref="ConflictException">Món đã có trong đơn (→ 409).</exception>
    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        Product product = await products.FindAsync(id, ct) ?? throw new NotFoundException($"Không có món {id}");

        if (await products.IsSoldAsync(id, ct))
        {
            // Giữ lịch sử bán hàng: món đã bán chỉ được đánh dấu "tạm hết"
            throw new ConflictException(
                $"{product.Name} đã có trong đơn hàng. Hãy đánh dấu tạm hết thay vì xóa.", "Không xóa được món");
        }

        products.Remove(product);
        await unitOfWork.SaveChangesAsync(ct); // DELETE FROM Products WHERE Id = @p0
    }

    // ⚠️ Lỗi hay gặp: gọi menuCache.InvalidateAsync() trong từng method ghi ở đây VÀ giữ filter
    //    [InvalidateMenuCache] ở controller → xóa cache 2 lần. b48 giữ nguyên filter của b47 (không đổi hành vi).
}
