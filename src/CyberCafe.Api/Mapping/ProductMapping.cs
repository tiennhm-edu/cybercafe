// ============================================================================
// ProductMapping.cs — Domain Product ⇄ DTO (Buổi 32–41 · projection + DTO đa hình).
// Hai chiều:
//   - Đọc:  Product → ProductDto bằng Expression (EF dịch thành SELECT chỉ các cột cần).
//   - Ghi:  ProductRequest → new Coffee/Tea/Cake (đi qua constructor + validate của domain).
// ============================================================================
using System.Linq.Expressions;
using CyberCafe.Contracts.Products;
using CyberCafe.Domain.Products;

namespace CyberCafe.Api.Mapping;

/// <summary>Chuyển đổi giữa domain Product và các DTO của API.</summary>
public static class ProductMapping
{
    // 👉 Bước 6 (b40.md): PROJECTION.
    // Expression<Func<...>> (không phải Func): EF đọc được "cây biểu thức" và dịch sang SQL:
    //   SELECT p.Id, p.ProductType, p.Name, p.Price, p.BeanType, p.TeaType, p.Flavor, ... FROM Products p
    // "p is Coffee" được dịch thành điều kiện trên cột discriminator ProductType = 'coffee'.
    // ⚠️ Lỗi hay gặp: dùng p.Category trong projection → Category là code C# (override), EF không dịch được
    //    sang SQL. Ở đây tự suy loại bằng "is", còn Category tính ở DTO (ProductTypes.CategoryOf).
    /// <summary>Biểu thức chiếu Product → ProductDto, dùng trong IQueryable.Select(...).</summary>
    public static readonly Expression<Func<Product, ProductDto>> ToDtoExpression = p => new ProductDto(
        p.Id,
        p is Coffee ? ProductTypes.Coffee : p is Tea ? ProductTypes.Tea : ProductTypes.Cake,
        p.Name,
        p.Price,
        p is Coffee ? ((Coffee)p).BeanType : p is Tea ? ((Tea)p).TeaType : p is Cake ? ((Cake)p).Flavor : string.Empty,
        p.Description,
        p.Emoji,
        p.IsAvailable);

    // Compile 1 lần thành delegate thường để map object đã có sẵn trong RAM (sau khi Add/SaveChanges).
    private static readonly Func<Product, ProductDto> ToDtoFunc = ToDtoExpression.Compile();

    /// <summary>Map 1 object Product đang có trong bộ nhớ.</summary>
    public static ProductDto ToDto(this Product product) => ToDtoFunc(product);

    /// <summary>"coffee" / "tea" / "cake" theo kiểu thật của object (pattern matching).</summary>
    public static string TypeOf(Product product) => product switch
    {
        Coffee => ProductTypes.Coffee,
        Tea => ProductTypes.Tea,
        _ => ProductTypes.Cake
    };

    /// <summary>
    /// Tạo món mới từ request. Constructor của domain validate tên/giá/loại hạt
    /// → dữ liệu sai sẽ ném <see cref="ArgumentException"/> (controller đổi thành 400).
    /// </summary>
    public static Product ToDomain(this ProductRequest request)
    {
        Product product = request.Type switch
        {
            ProductTypes.Coffee => new Coffee(request.Name, request.Price, request.Variant),
            ProductTypes.Tea => new Tea(request.Name, request.Price, request.Variant),
            ProductTypes.Cake => new Cake(request.Name, request.Price, request.Variant),
            _ => throw new ArgumentException($"Loại món '{request.Type}' không hợp lệ")
        };

        ApplyCommon(product, request);
        return product;
    }

    /// <summary>
    /// Ghi đè dữ liệu request lên món ĐANG ĐƯỢC THEO DÕI (tracked) — EF tự phát hiện field nào đổi
    /// và chỉ UPDATE các cột đó khi SaveChanges (change tracking).
    /// </summary>
    public static void ApplyTo(this ProductRequest request, Product product)
    {
        product.Name = request.Name;
        product.UpdatePrice(request.Price); // Price có private set → đổi qua method của domain

        // switch trên KIỂU: mỗi loại có property riêng cho "Variant"
        switch (product)
        {
            case Coffee coffee:
                coffee.BeanType = request.Variant;
                break;
            case Tea tea:
                tea.TeaType = request.Variant;
                break;
            case Cake cake:
                cake.Flavor = request.Variant;
                break;
        }

        ApplyCommon(product, request);
    }

    private static void ApplyCommon(Product product, ProductRequest request)
    {
        product.Description = request.Description?.Trim() ?? string.Empty;
        product.Emoji = string.IsNullOrWhiteSpace(request.Emoji) ? "🍽️" : request.Emoji.Trim();
        product.IsAvailable = request.IsAvailable;
    }
}
