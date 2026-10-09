// ============================================================================
// OrderStore.cs — kho đơn hàng in-memory (Buổi 24–31 · Singleton + thread safety).
// So sánh với CartState: giỏ hàng là của riêng từng tab (Scoped), còn danh sách đơn
// là của cả quán (Singleton) — quầy barista sau này cần thấy đơn của mọi khách.
// ============================================================================
using CyberCafe.Domain.Orders;

namespace CyberCafe.Web.Services;

/// <summary>
/// Kho đơn hàng in-memory — Singleton: mọi người dùng (mọi circuit) thấy chung.
/// Có lock vì nhiều circuit có thể đặt đơn cùng lúc.
/// Buổi 41 sẽ thay bằng EF Core + SQL Server.
/// </summary>
public class OrderStore
{
    private readonly List<Order> _orders = [];
    // System.Threading.Lock (.NET 9+): object khóa chuyên dụng cho câu lệnh lock
    private readonly Lock _lock = new();
    private int _sequence;

    /// <summary>
    /// Lưu đơn và gán mã tăng dần CC-0001, CC-0002...
    /// ⚠️ Không có lock: 2 khách đặt cùng lúc có thể nhận TRÙNG mã (race condition)
    /// vì List và _sequence++ không an toàn đa luồng.
    /// </summary>
    public Order Add(Order order)
    {
        lock (_lock)
        {
            _sequence++;
            // :0000 = định dạng đủ 4 chữ số, thêm 0 phía trước
            order.Id = $"CC-{_sequence:0000}";
            _orders.Add(order);
            return order;
        }
    }

    /// <summary>Tìm đơn theo mã, không phân biệt hoa/thường; không thấy trả về null.</summary>
    public Order? GetById(string id)
    {
        lock (_lock)
        {
            return _orders.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>Tất cả đơn, mới nhất trước. ToList() tạo bản sao → duyệt bên ngoài lock vẫn an toàn.</summary>
    public IReadOnlyList<Order> GetAll()
    {
        lock (_lock)
        {
            return _orders.OrderByDescending(x => x.CreatedAt).ToList();
        }
    }
}
