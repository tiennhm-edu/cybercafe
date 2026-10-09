// ============================================================================
// MenuService.cs — service cung cấp dữ liệu menu (Buổi 23 tạo · Buổi 24–31 đổi sang Domain).
// Component KHÔNG tự new MenuService(); chúng "xin" qua @inject, DI container
// đưa instance đã đăng ký. Nhờ vậy sau này đổi nguồn dữ liệu (API, database)
// chỉ phải sửa ở đây, các trang Razor giữ nguyên.
// Buổi 24–31: danh sách giờ là List<Product> chứa Coffee/Tea/Cake — đa hình (polymorphism).
// ============================================================================
using CyberCafe.Domain.Products;

namespace CyberCafe.Web.Services;

/// <summary>
/// Cung cấp dữ liệu menu từ seed in-memory (chưa có database).
/// Buổi 28: thay model tạm MenuItem bằng hierarchy OOP Product → Coffee/Tea/Cake.
/// Buổi 32 sẽ thay bằng typed HttpClient gọi CyberCafe.Api.
/// </summary>
public class MenuService
{
    // Dữ liệu mẫu (seed). Collection expression [ ... ] (C# 12).
    // Khai báo kiểu cha List<Product> nhưng chứa object của class con (Coffee, Tea, Cake):
    // gọi product.GetPrice(size) / product.Category sẽ chạy đúng phiên bản override của từng loại.
    // Constructor nhận dữ liệu bắt buộc (tên, giá, loại hạt...), object initializer { ... } gán phần còn lại.
    // Service là Singleton → danh sách này tồn tại suốt vòng đời app, mọi người dùng đọc chung.
    private readonly List<Product> _products =
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

    /// <summary>Toàn bộ món. Trả về <c>IReadOnlyList</c> để nơi gọi không Add/Remove vào list gốc.</summary>
    public IReadOnlyList<Product> GetAll() => _products;

    /// <summary>Danh sách nhóm món không trùng lặp, giữ thứ tự xuất hiện (LINQ Select + Distinct).</summary>
    public IReadOnlyList<string> GetCategories() =>
        _products.Select(x => x.Category).Distinct().ToList();

    /// <summary>Các món thuộc 1 nhóm (LINQ Where). Category là property virtual do từng class con override.</summary>
    public IReadOnlyList<Product> GetByCategory(string category) =>
        _products.Where(x => x.Category == category).ToList();

    /// <summary>Tìm món theo Id; không thấy trả về <c>null</c> (vì vậy kiểu trả về là <c>Product?</c>).</summary>
    public Product? GetById(int id) => _products.FirstOrDefault(x => x.Id == id);

    /// <summary>
    /// Bản async giả lập độ trễ mạng — để demo OnInitializedAsync + trạng thái "Đang tải...".
    /// </summary>
    /// <param name="cancellationToken">Token để hủy chờ khi người dùng rời trang (Menu.razor hủy trong Dispose).</param>
    public async Task<IReadOnlyList<Product>> GetMenuAsync(CancellationToken cancellationToken = default)
    {
        // Task.Delay thay cho lời gọi HTTP/database thật; ném OperationCanceledException nếu bị hủy
        await Task.Delay(400, cancellationToken);
        return _products;
    }
}
