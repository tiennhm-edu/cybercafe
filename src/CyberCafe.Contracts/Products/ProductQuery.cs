// ============================================================================
// ProductQuery.cs — tham số lọc/sắp xếp/phân trang của GET /api/products (Buổi 32–41 · LINQ).
// Api: [FromQuery] ProductQuery → ASP.NET Core đọc ?search=...&page=2 vào các property.
// Web: ToQueryString() dựng đúng URL đó → 2 phía không lệch tên tham số.
// ============================================================================
using System.ComponentModel.DataAnnotations;

namespace CyberCafe.Contracts.Products;

/// <summary>Bộ lọc thực đơn: tìm theo tên, loại, còn hàng; sắp xếp; phân trang.</summary>
public class ProductQuery
{
    /// <summary>Kích thước trang tối đa — chặn client xin 1 triệu dòng một lần.</summary>
    public const int MaxPageSize = 100;

    /// <summary>Từ khóa tìm trong tên món.</summary>
    [StringLength(50)]
    public string? Search { get; set; }

    /// <summary>Lọc theo loại: coffee | tea | cake (để trống = tất cả).</summary>
    [RegularExpression(ProductTypes.Pattern, ErrorMessage = "Loại món phải là coffee, tea hoặc cake")]
    public string? Type { get; set; }

    /// <summary>true = chỉ món còn hàng; null = tất cả.</summary>
    public bool? Available { get; set; }

    /// <summary>Sắp xếp theo: id | name | price.</summary>
    [RegularExpression("^(id|name|price)$", ErrorMessage = "sortBy phải là id, name hoặc price")]
    public string SortBy { get; set; } = "id";

    /// <summary>true = giảm dần.</summary>
    public bool Desc { get; set; }

    /// <summary>Số trang, bắt đầu từ 1.</summary>
    [Range(1, 10_000, ErrorMessage = "page phải ≥ 1")]
    public int Page { get; set; } = 1;

    /// <summary>Số món mỗi trang (1–100).</summary>
    [Range(1, MaxPageSize, ErrorMessage = "pageSize từ 1 đến 100")]
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// Dựng query string, vd "page=1&amp;pageSize=20&amp;sortBy=id&amp;search=tr%C3%A0".
    /// Thứ tự cố định → dùng luôn làm KEY CACHE ở API (Buổi 42–47).
    /// ⚠️ Lỗi hay gặp: nối thẳng chữ người dùng gõ vào URL → "trà & bánh" làm vỡ query string. Luôn EscapeDataString.
    /// </summary>
    public string ToQueryString()
    {
        List<string> parts = [$"page={this.Page}", $"pageSize={this.PageSize}", $"sortBy={this.SortBy}"];
        if (this.Desc)
        {
            parts.Add("desc=true");
        }

        if (!string.IsNullOrWhiteSpace(this.Type))
        {
            parts.Add($"type={Uri.EscapeDataString(this.Type)}");
        }

        if (this.Available is not null)
        {
            parts.Add($"available={this.Available.Value.ToString().ToLowerInvariant()}");
        }

        if (!string.IsNullOrWhiteSpace(this.Search))
        {
            parts.Add($"search={Uri.EscapeDataString(this.Search.Trim())}");
        }

        return string.Join('&', parts);
    }
}
