// ============================================================================
// ProductDto.cs — dữ liệu 1 món trả về từ API (Buổi 32–41 · DTO).
// Vì sao không trả thẳng object domain Coffee/Tea/Cake ra JSON?
//   - Domain có logic + validate trong setter; JSON chỉ cần dữ liệu phẳng.
//   - Đổi tên field trong domain không làm "vỡ" client đang dùng API (DTO là hợp đồng ổn định).
//   - Kiểm soát được những gì lộ ra ngoài (không vô tình trả cột nội bộ).
// 👉 Bước 2 (b40.md): DTO nằm trong project Contracts → Api trả ra, Web đọc vào, CÙNG 1 định nghĩa.
// ============================================================================
namespace CyberCafe.Contracts.Products;

/// <summary>Một món trên thực đơn (JSON của GET /api/products).</summary>
/// <param name="Id">Mã món.</param>
/// <param name="Type">"coffee" | "tea" | "cake" (xem <see cref="ProductTypes"/>).</param>
/// <param name="Name">Tên món.</param>
/// <param name="Price">Giá cơ bản (size S / món không size), VND.</param>
/// <param name="Variant">Loại hạt (cà phê) / loại trà (trà) / hương vị (bánh).</param>
/// <param name="Description">Mô tả ngắn.</param>
/// <param name="Emoji">Emoji minh họa.</param>
/// <param name="IsAvailable">false = tạm hết.</param>
public record ProductDto(
    int Id,
    string Type,
    string Name,
    decimal Price,
    string Variant,
    string Description,
    string Emoji,
    bool IsAvailable)
{
    // Property chỉ có get vẫn được ghi ra JSON (tiện cho client JS), nhưng bị bỏ qua khi đọc vào
    /// <summary>Nhóm hiển thị ("Cà phê", "Trà", "Bánh"), suy ra từ Type.</summary>
    public string Category => ProductTypes.CategoryOf(this.Type);

    /// <summary>Món có chọn size không, suy ra từ Type.</summary>
    public bool HasSize => ProductTypes.HasSize(this.Type);
}
