// ============================================================================
// ProductTypes.cs — tên loại món trong JSON (Buổi 32–41 · DTO đa hình).
// JSON không biết class Coffee/Tea/Cake → thêm trường "type": "coffee" | "tea" | "cake".
// Cùng giá trị này được dùng làm DISCRIMINATOR của bảng Products (TPH) trong EF Core.
// ============================================================================
namespace CyberCafe.Contracts.Products;

/// <summary>Hằng số loại món + thông tin hiển thị suy ra từ loại.</summary>
public static class ProductTypes
{
    /// <summary>Cà phê (có size).</summary>
    public const string Coffee = "coffee";

    /// <summary>Trà (có size).</summary>
    public const string Tea = "tea";

    /// <summary>Bánh (không size).</summary>
    public const string Cake = "cake";

    /// <summary>Tất cả loại hợp lệ — dùng để validate và vẽ dropdown.</summary>
    public static readonly IReadOnlyList<string> All = [Coffee, Tea, Cake];

    /// <summary>Regex cho [RegularExpression] (attribute chỉ nhận hằng số).</summary>
    public const string Pattern = "^(coffee|tea|cake)$";

    /// <summary>Nhóm hiển thị trên menu — khớp với Product.Category ở Domain.</summary>
    public static string CategoryOf(string type) => type switch
    {
        Coffee => "Cà phê",
        Tea => "Trà",
        Cake => "Bánh",
        _ => "Khác"
    };

    /// <summary>Đồ uống mới có size.</summary>
    public static bool HasSize(string type) => type is Coffee or Tea;

    /// <summary>Nhãn của trường "Variant" theo loại: loại hạt / loại trà / hương vị.</summary>
    public static string VariantLabel(string type) => type switch
    {
        Coffee => "Loại hạt",
        Tea => "Loại trà",
        _ => "Hương vị"
    };
}
