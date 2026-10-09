// ============================================================================
// MenuSeed.cs — dữ liệu thực đơn mẫu (Buổi 23–31 nằm trong Web/MenuService;
//               Buổi 36–41 chuyển sang Api để seed vào database bằng HasData;
//               Buổi 48 chuyển cùng DbContext sang Infrastructure/Persistence).
// HasData được "chụp" vào migration InitialCreate thành các câu INSERT.
// ⚠️ Lỗi hay gặp: sửa dữ liệu ở đây mà không tạo migration mới → database KHÔNG đổi theo.
// ============================================================================
using CyberCafe.Domain.Products;

namespace CyberCafe.Infrastructure.Persistence;

/// <summary>8 món mẫu: 3 cà phê, 3 trà (1 món tạm hết), 2 bánh.</summary>
public static class MenuSeed
{
    // Id viết tay (1..8): HasData BẮT BUỘC có khóa chính để EF so sánh giữa các migration.
    // Danh sách kiểu cha List<Product> nhưng chứa Coffee/Tea/Cake (đa hình như buổi 24–31).
    /// <summary>Các món mẫu. Mỗi lần gọi trả về object MỚI (EF và test không dùng chung instance).</summary>
    public static IReadOnlyList<Product> Products() =>
    [
        new Coffee("Cà phê sữa đá", 29000, "Robusta") { Id = 1, Emoji = "☕", Description = "Robusta Đắk Lắk + sữa đặc" },
        new Coffee("Bạc xỉu", 32000, "Robusta") { Id = 2, Emoji = "🥛", Description = "Nhiều sữa, ít cà phê" },
        new Coffee("Americano", 35000, "Arabica") { Id = 3, Emoji = "☕", Description = "Espresso pha loãng" },
        new Tea("Trà đào cam sả", 39000, "Trà đen") { Id = 4, Emoji = "🍑", Description = "Trà đen, đào miếng, cam, sả" },
        new Tea("Trà sen vàng", 42000, "Ô long") { Id = 5, Emoji = "🪷", Description = "Trà ô long, hạt sen, kem sữa" },
        new Tea("Matcha latte", 45000, "Matcha") { Id = 6, Emoji = "🍵", Description = "Matcha Nhật + sữa tươi", IsAvailable = false },
        new Cake("Bánh tiramisu", 35000, "Cà phê") { Id = 7, Emoji = "🍰", Description = "Vị cà phê, cacao" },
        new Cake("Croissant bơ", 25000, "Bơ") { Id = 8, Emoji = "🥐", Description = "Bơ Pháp, nướng mỗi sáng" },
    ];
}
