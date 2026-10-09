// ============================================================================
// MenuService.cs — service cung cấp dữ liệu menu (Buổi 23 · Dependency Injection).
// Component KHÔNG tự new MenuService(); chúng "xin" qua @inject, DI container
// đưa instance đã đăng ký. Nhờ vậy sau này đổi nguồn dữ liệu (API, database)
// chỉ phải sửa ở đây, các trang Razor giữ nguyên.
// ============================================================================
using CyberCafe.Web.Models;

namespace CyberCafe.Web.Services;

/// <summary>
/// Cung cấp dữ liệu menu từ seed in-memory (chưa có database).
/// Đăng ký DI trong Program.cs: builder.Services.AddSingleton&lt;MenuService&gt;();
/// Buổi 32 sẽ thay bằng HttpClient gọi CyberCafe.Api.
/// </summary>
public class MenuService
{
    // Dữ liệu mẫu (seed). Collection expression [ ... ] + target-typed new() (C# 12).
    // Service là Singleton → danh sách này tồn tại suốt vòng đời app, mọi người dùng đọc chung.
    private readonly List<MenuItem> _items =
    [
        new() { Id = 1, Name = "Cà phê sữa đá", Category = "Cà phê", Price = 29000, Emoji = "☕", Description = "Robusta Đắk Lắk + sữa đặc" },
        new() { Id = 2, Name = "Bạc xỉu", Category = "Cà phê", Price = 32000, Emoji = "🥛", Description = "Nhiều sữa, ít cà phê" },
        new() { Id = 3, Name = "Americano", Category = "Cà phê", Price = 35000, Emoji = "☕", Description = "Espresso pha loãng" },
        new() { Id = 4, Name = "Trà đào cam sả", Category = "Trà", Price = 39000, Emoji = "🍑", Description = "Trà đen, đào miếng, cam, sả" },
        new() { Id = 5, Name = "Trà sen vàng", Category = "Trà", Price = 42000, Emoji = "🪷", Description = "Trà ô long, hạt sen, kem sữa" },
        new() { Id = 6, Name = "Matcha latte", Category = "Trà", Price = 45000, Emoji = "🍵", Description = "Matcha Nhật + sữa tươi", IsAvailable = false },
        new() { Id = 7, Name = "Bánh tiramisu", Category = "Bánh", Price = 35000, Emoji = "🍰", Description = "Vị cà phê, cacao" },
        new() { Id = 8, Name = "Croissant bơ", Category = "Bánh", Price = 25000, Emoji = "🥐", Description = "Bơ Pháp, nướng mỗi sáng" },
    ];

    /// <summary>Toàn bộ món. Trả về <c>IReadOnlyList</c> để nơi gọi không Add/Remove vào list gốc.</summary>
    public IReadOnlyList<MenuItem> GetAll() => _items;

    /// <summary>Danh sách nhóm món không trùng lặp, giữ thứ tự xuất hiện (LINQ Select + Distinct).</summary>
    public IReadOnlyList<string> GetCategories() =>
        _items.Select(x => x.Category).Distinct().ToList();

    /// <summary>Các món thuộc 1 nhóm (LINQ Where).</summary>
    public IReadOnlyList<MenuItem> GetByCategory(string category) =>
        _items.Where(x => x.Category == category).ToList();

    /// <summary>Tìm món theo Id; không thấy trả về <c>null</c> (vì vậy kiểu trả về là <c>MenuItem?</c>).</summary>
    public MenuItem? GetById(int id) => _items.FirstOrDefault(x => x.Id == id);
}
