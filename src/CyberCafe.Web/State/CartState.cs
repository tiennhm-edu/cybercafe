// ============================================================================
// CartState.cs — state giỏ hàng dùng chung giữa các component (Buổi 24–31 · State management).
//
// Vấn đề: Menu thêm món, nhưng CartSummary (trên layout) và CartPage cũng phải thấy.
// Các component này không phải cha–con của nhau → không truyền [Parameter] được.
// Giải pháp: 1 service giữ state, đăng ký SCOPED trong DI:
//   - Blazor Server: 1 instance cho mỗi circuit (= 1 tab) → mọi component trong tab
//     dùng chung 1 giỏ, còn người dùng khác có giỏ riêng.
//   - Đổi state chỉ qua method → method bắn event OnChange → ai subscribe thì tự render lại.
// Lưu ý: state nằm trong RAM của server; F5 tải lại trang = circuit mới = giỏ mới (trống).
// Buổi 32–41: nhớ thêm MÃ giảm giá (DiscountCode) để gửi lên API khi đặt hàng —
// API chỉ tin mã, không tin số tiền giảm do Web tự tính.
// ============================================================================
using CyberCafe.Domain.Discounts;
using CyberCafe.Domain.Orders;
using CyberCafe.Domain.Products;

namespace CyberCafe.Web.State;

/// <summary>
/// State giỏ hàng dùng chung giữa các component (ProductCard, CartSummary, trang Cart...).
/// Đăng ký Scoped → Blazor Server: 1 instance / 1 circuit (1 tab trình duyệt).
/// Pattern: mọi thay đổi đi qua method → gọi NotifyStateChanged() → bắn event OnChange.
/// Component nào quan tâm thì subscribe OnChange và gọi StateHasChanged; nhớ unsubscribe trong Dispose().
/// </summary>
public class CartState
{
    /// <summary>
    /// Giỏ hàng domain (pure C#). Component được ĐỌC thoải mái.
    /// ⚠️ Lỗi hay gặp: sửa trực tiếp CartState.Cart.AddItem(...) → giỏ đổi nhưng KHÔNG bắn OnChange,
    /// badge CartSummary đứng yên. Luôn sửa qua các method của CartState.
    /// </summary>
    public Cart Cart { get; } = new();

    /// <summary>
    /// Bắn ra sau MỖI thay đổi của giỏ. <c>Action?</c>: có thể null khi chưa ai subscribe.
    /// Subscribe: <c>CartState.OnChange += Handler;</c> — nhớ <c>-=</c> trong Dispose().
    /// </summary>
    public event Action? OnChange;

    /// <summary>Mã giảm giá đang áp dụng (đã chuẩn hóa, vd "GIAM20K"); null = không có. Gửi kèm khi đặt hàng.</summary>
    public string? DiscountCode { get; private set; }

    /// <summary>Thêm món vào giỏ rồi thông báo thay đổi.</summary>
    public void AddItem(Product product, DrinkSize size, int quantity)
    {
        Cart.AddItem(product, size, quantity);
        NotifyStateChanged();
    }

    /// <summary>Đổi số lượng (≤ 0 = xóa dòng) rồi thông báo.</summary>
    public void UpdateQuantity(OrderItem item, int quantity)
    {
        Cart.UpdateQuantity(item, quantity);
        NotifyStateChanged();
    }

    /// <summary>Xóa 1 dòng rồi thông báo.</summary>
    public void RemoveItem(OrderItem item)
    {
        Cart.RemoveItem(item);
        NotifyStateChanged();
    }

    /// <summary>
    /// Tra mã trong <see cref="DiscountCatalog"/> (Domain, dùng chung với Api); hợp lệ thì áp dụng + thông báo.
    /// Trả về false nếu mã không tồn tại (giỏ giữ nguyên).
    /// </summary>
    public bool ApplyDiscountCode(string? code)
    {
        // DiscountCatalog trả về Discount (class cha) — CartState không cần biết là Voucher hay Member
        Discount? discount = DiscountCatalog.FindByCode(code);
        if (discount is null)
        {
            return false;
        }

        Cart.ApplyDiscount(discount);
        DiscountCode = DiscountCatalog.Normalize(code);
        NotifyStateChanged();
        return true;
    }

    /// <summary>Bỏ giảm giá rồi thông báo.</summary>
    public void ClearDiscount()
    {
        Cart.ClearDiscount();
        DiscountCode = null;
        NotifyStateChanged();
    }

    /// <summary>Làm trống giỏ (sau khi đặt hàng) rồi thông báo.</summary>
    public void Clear()
    {
        Cart.Clear();
        DiscountCode = null;
        NotifyStateChanged();
    }

    // ?.Invoke(): chỉ gọi khi đã có người subscribe (tránh NullReferenceException)
    private void NotifyStateChanged() => OnChange?.Invoke();
}
