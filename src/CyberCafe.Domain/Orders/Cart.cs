// ============================================================================
// Cart.cs — giỏ hàng thuần C# (Buổi 24–31 · Domain model, Encapsulation).
// Vì sao để Cart trong Domain chứ không viết thẳng trong component Blazor?
//   - Logic tính tiền (gộp dòng, phụ thu size, giảm giá) test được bằng xUnit ngay.
//   - Không phụ thuộc UI: sau này Web API / app mobile dùng lại nguyên class này.
// Phần "báo cho UI biết giỏ đã đổi" là việc của CartState (project Web).
// ============================================================================
using CyberCafe.Domain.Discounts;
using CyberCafe.Domain.People;
using CyberCafe.Domain.Products;

namespace CyberCafe.Domain.Orders;

/// <summary>
/// Giỏ hàng: danh sách OrderItem + 1 Discount (tùy chọn).
/// Pure C# — không biết gì về Blazor. Blazor bọc nó bằng CartState để thông báo UI.
/// Khi checkout: ToOrder(customer, note) → Order.
/// </summary>
public class Cart
{
    private readonly List<OrderItem> _items = [];

    // Bên ngoài chỉ đọc; thao tác qua method (Encapsulation)
    /// <summary>Các dòng trong giỏ (chỉ đọc). Muốn thêm/xóa phải qua AddItem/RemoveItem.</summary>
    public IReadOnlyList<OrderItem> Items => this._items;

    /// <summary>Giảm giá đang áp dụng (null = không có). <c>private set</c>: chỉ đổi qua ApplyDiscount/ClearDiscount.</summary>
    public Discount? Discount { get; private set; }

    /// <summary>Giỏ trống hay không.</summary>
    public bool IsEmpty => this._items.Count == 0;

    /// <summary>Tổng số món (cộng Quantity của mọi dòng), không phải số dòng.</summary>
    public int ItemCount => this._items.Sum(x => x.Quantity);

    // Tổng tiền gốc (đã tính phụ thu size)
    /// <summary>Tổng tiền trước giảm giá. Computed property: luôn tính lại, không lưu → không bao giờ "lệch".</summary>
    public decimal Subtotal => this._items.Sum(x => x.TotalPrice);

    /// <summary>Số tiền được giảm; <c>?.</c> và <c>??</c>: không có Discount thì bằng 0.</summary>
    public decimal DiscountAmount => this.Discount?.GetDiscountAmount(this.Subtotal) ?? 0;

    /// <summary>Số tiền phải trả = Subtotal − DiscountAmount.</summary>
    public decimal Total => this.Subtotal - this.DiscountAmount;

    // Cùng Product + cùng Size → cộng dồn số lượng, khác size → dòng mới
    /// <summary>
    /// Thêm món vào giỏ. Cùng món + cùng size thì tăng số lượng dòng cũ, ngược lại tạo dòng mới.
    /// Món không có size (bánh) luôn được tính là size S.
    /// </summary>
    /// <exception cref="InvalidOperationException">Món đang tạm hết.</exception>
    public OrderItem AddItem(Product product, DrinkSize size = DrinkSize.S, int quantity = 1)
    {
        ArgumentNullException.ThrowIfNull(product);

        if (!product.IsAvailable)
        {
            throw new InvalidOperationException($"{product.Name} đang tạm hết");
        }

        DrinkSize effectiveSize = product.HasSize ? size : DrinkSize.S;
        OrderItem? existing = this.FindItem(product, effectiveSize);

        if (existing != null)
        {
            existing.IncreaseQuantity(quantity);
            return existing;
        }

        OrderItem item = new(product, effectiveSize, quantity);
        this._items.Add(item);
        return item;
    }

    /// <summary>Tìm dòng theo món (so Id) + size; không có trả về null.</summary>
    public OrderItem? FindItem(Product product, DrinkSize size)
    {
        return this._items.FirstOrDefault(x => x.Product.Id == product.Id && x.Size == size);
    }

    // Số lượng ≤ 0 → xóa dòng
    /// <summary>Đổi số lượng của 1 dòng; số lượng ≤ 0 nghĩa là xóa dòng đó.</summary>
    public void UpdateQuantity(OrderItem item, int quantity)
    {
        if (quantity <= 0)
        {
            this.RemoveItem(item);
            return;
        }

        item.Quantity = quantity;
    }

    /// <summary>Xóa 1 dòng; trả về true nếu xóa được.</summary>
    public bool RemoveItem(OrderItem item)
    {
        return this._items.Remove(item);
    }

    /// <summary>
    /// Áp dụng giảm giá. Tham số kiểu cha <see cref="Discounts.Discount"/> nên nhận được mọi loại
    /// (MemberDiscount, VoucherDiscount...) mà Cart không cần biết là loại nào.
    /// </summary>
    public void ApplyDiscount(Discount discount)
    {
        this.Discount = discount ?? throw new ArgumentNullException(nameof(discount));
    }

    /// <summary>Bỏ giảm giá đang áp dụng.</summary>
    public void ClearDiscount()
    {
        this.Discount = null;
    }

    /// <summary>Làm trống giỏ (gọi sau khi đặt hàng thành công).</summary>
    public void Clear()
    {
        this._items.Clear();
        this.Discount = null;
    }

    /// <summary>Chốt giỏ thành <see cref="Order"/> (Order tự sao chép các dòng).</summary>
    /// <exception cref="InvalidOperationException">Giỏ trống.</exception>
    public Order ToOrder(Customer customer, string? note = null)
    {
        if (this.IsEmpty)
        {
            throw new InvalidOperationException("Giỏ hàng đang trống");
        }

        return new Order(customer, this._items, this.Discount, note);
    }
}
