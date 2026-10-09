// ============================================================================
// OrderItem.cs — 1 dòng trong giỏ/đơn (Buổi 24–31 · Association + computed property).
// ============================================================================
using CyberCafe.Domain.Products;

namespace CyberCafe.Domain.Orders;

/// <summary>
/// 1 dòng trong giỏ / đơn: Product + Size + Quantity.
/// Association: OrderItem gắn với Product.
/// </summary>
public class OrderItem
{
    /// <summary>Món được chọn (tham chiếu tới object Product trong menu).</summary>
    public Product Product { get; }

    // Món không có size (bánh) luôn là S
    /// <summary>Size đã chọn; món không có size luôn là S.</summary>
    public DrinkSize Size { get; }

    private int _quantity;

    /// <summary>Số lượng, phải &gt; 0 (muốn xóa dòng thì dùng Cart.RemoveItem / UpdateQuantity(0)).</summary>
    public int Quantity
    {
        get => this._quantity;
        set
        {
            if (value <= 0)
            {
                throw new ArgumentException("Số lượng phải lớn hơn 0");
            }

            this._quantity = value;
        }
    }

    // Computed property: đơn giá theo size (đa hình qua Product.GetPrice)
    /// <summary>Đơn giá theo size. Coffee/Tea cộng phụ thu, Cake trả giá gốc — OrderItem không cần biết loại.</summary>
    public decimal UnitPrice => this.Product.GetPrice(this.Size);

    /// <summary>Thành tiền của dòng = đơn giá × số lượng.</summary>
    public decimal TotalPrice => this.UnitPrice * this.Quantity;

    /// <summary>Tạo dòng mới; size bị bỏ qua (về S) nếu món không có size.</summary>
    public OrderItem(Product product, DrinkSize size, int quantity)
    {
        this.Product = product ?? throw new ArgumentNullException(nameof(product));
        this.Size = product.HasSize ? size : DrinkSize.S;
        this.Quantity = quantity;
    }

    /// <summary>Tăng số lượng thêm <paramref name="amount"/> (mặc định 1).</summary>
    public void IncreaseQuantity(int amount = 1)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Số lượng tăng phải lớn hơn 0");
        }

        this.Quantity += amount;
    }

    /// <summary>Chuỗi mô tả dòng, vd "Bạc xỉu (size M) x 2 = 74.000 đ".</summary>
    public string Display()
    {
        string size = this.Product.HasSize ? $" (size {this.Size})" : string.Empty;
        return $"{this.Product.Name}{size} x {this.Quantity} = {this.TotalPrice:N0} đ";
    }
}
