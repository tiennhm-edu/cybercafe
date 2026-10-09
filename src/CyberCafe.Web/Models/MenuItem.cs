// ============================================================================
// MenuItem.cs — model dữ liệu đơn giản cho 1 món (Buổi 23 · Model/POCO).
// Chỉ gồm property get/set, chưa có logic → dễ hiển thị bằng Razor.
// ============================================================================
namespace CyberCafe.Web.Models;

/// <summary>
/// Model tạm cho buổi 23: 1 món trong menu.
/// Buổi 28 sẽ thay bằng hierarchy OOP (Product → Coffee/Tea/Cake) trong CyberCafe.Domain.
/// </summary>
public class MenuItem
{
    /// <summary>Mã món (duy nhất trong menu).</summary>
    public int Id { get; set; }

    /// <summary>Tên hiển thị. Gán sẵn <c>string.Empty</c> để không bị null (Nullable đang bật).</summary>
    public string Name { get; set; } = string.Empty;

    // "Cà phê", "Trà", "Bánh"
    /// <summary>Nhóm món — trang Menu dùng để gom nhóm.</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Giá bán (VND). Dùng <c>decimal</c> cho tiền, không dùng double (tránh sai số làm tròn).</summary>
    public decimal Price { get; set; }

    /// <summary>Mô tả ngắn.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Emoji minh họa thay cho ảnh.</summary>
    public string Emoji { get; set; } = "☕";

    /// <summary><c>false</c> = tạm hết hàng (card hiển thị mờ).</summary>
    public bool IsAvailable { get; set; } = true;
}
