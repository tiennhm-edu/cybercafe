// ============================================================================
// ProductRequest.cs — body của POST/PUT /api/products (Buổi 32–41 · validate 2 phía).
// Cùng 1 class được dùng ở:
//   - Api: [ApiController] tự validate DataAnnotations → sai thì trả 400 ValidationProblem.
//   - Web: trang /admin/menu dùng làm Model của EditForm → báo lỗi ngay trên form.
// Validate ở client cho trải nghiệm tốt; validate ở server mới là BẮT BUỘC (client có thể bị bỏ qua).
// ============================================================================
using System.ComponentModel.DataAnnotations;

namespace CyberCafe.Contracts.Products;

/// <summary>Dữ liệu tạo/sửa 1 món. Class (không phải record) vì EditForm cần property có setter.</summary>
public class ProductRequest
{
    /// <summary>"coffee" | "tea" | "cake". Không đổi được sau khi tạo (đổi loại = xóa + tạo mới).</summary>
    [Required(ErrorMessage = "Vui lòng chọn loại món")]
    [RegularExpression(ProductTypes.Pattern, ErrorMessage = "Loại món phải là coffee, tea hoặc cake")]
    public string Type { get; set; } = ProductTypes.Coffee;

    /// <summary>Tên món, 2–100 ký tự.</summary>
    [Required(ErrorMessage = "Vui lòng nhập tên món")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Tên món từ 2 đến 100 ký tự")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Giá cơ bản (VND), 1.000 – 1.000.000.</summary>
    [Range(1000, 1_000_000, ErrorMessage = "Giá từ 1.000 đến 1.000.000 đ")]
    public decimal Price { get; set; } = 30000;

    /// <summary>Loại hạt / loại trà / hương vị — bắt buộc (domain ném lỗi nếu rỗng).</summary>
    [Required(ErrorMessage = "Vui lòng nhập loại hạt / loại trà / hương vị")]
    [StringLength(50, ErrorMessage = "Tối đa 50 ký tự")]
    public string Variant { get; set; } = string.Empty;

    /// <summary>Mô tả ngắn, tối đa 300 ký tự.</summary>
    [StringLength(300, ErrorMessage = "Mô tả tối đa 300 ký tự")]
    public string? Description { get; set; }

    /// <summary>Emoji minh họa (để trống → 🍽️).</summary>
    [StringLength(16, ErrorMessage = "Emoji tối đa 16 ký tự")]
    public string? Emoji { get; set; }

    /// <summary>false = tạm hết.</summary>
    public bool IsAvailable { get; set; } = true;
}
