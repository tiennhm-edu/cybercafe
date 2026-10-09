// ============================================================================
// MenuService.cs — use case GHI thực đơn (Buổi 48 · Clean Architecture; Buổi 51–53: phần đọc → Queries/MenuQueries.cs).
// Trước (b47) toàn bộ logic này nằm trong ProductsController: LINQ trên DbContext, cache, kiểm tra "đã bán".
// Bây giờ controller chỉ còn: nhận HTTP → gọi MenuService → trả HTTP. MenuService thì:
//   - Không biết HTTP (không IActionResult, không StatusCodes) → lỗi = exception có nghĩa (AppExceptions.cs).
//   - Không biết EF Core / Redis → chỉ dùng port: IProductRepository, IUnitOfWork, IMenuCache.
//   → Unit test được bằng bản giả (fake) của 3 interface, không cần database.
// Hành vi GIỮ NGUYÊN 100% so với b47 (test tích hợp cũ chạy lại không sửa dòng nào ngoài using).
// Buổi 51–53: GetPage/GetById chuyển thành GetMenuQuery/GetProductByIdQuery (đi qua dispatcher + cache).
//   Thêm/sửa/xóa món là CRUD đơn giản → giữ service này, không cần command (ADR 0003: CQRS có chọn lọc).
// ============================================================================
using CyberCafe.Application.Common.Exceptions;
using CyberCafe.Application.Common.Interfaces;
using CyberCafe.Contracts.Products;
using CyberCafe.Domain.Products;

namespace CyberCafe.Application.Products;

// 👉 Bước 7 (b48.md)
/// <summary>Thêm / sửa / xóa thực đơn (cache được filter [InvalidateMenuCache] ở Api vô hiệu hóa sau khi ghi).</summary>
public class MenuService(IProductRepository products, IUnitOfWork unitOfWork)
{
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

    // ⚠️ Lỗi hay gặp: gọi IMenuCache.InvalidateAsync() trong từng method ghi ở đây VÀ giữ filter
    //    [InvalidateMenuCache] ở controller → xóa cache 2 lần. b48–b53 giữ nguyên filter của b47.
}
