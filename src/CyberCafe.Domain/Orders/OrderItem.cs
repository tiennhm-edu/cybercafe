// ============================================================================
// OrderItem.cs — 1 dòng trong giỏ/đơn (Buổi 24–31 · Association + computed property;
//                Buổi 32–41: chốt đơn giá lúc tạo dòng + constructor rỗng cho EF Core).
// Vì sao UnitPrice không còn tính "sống" từ Product.GetPrice(size)?
//   Đơn đã lưu DB phải giữ GIÁ LÚC ĐẶT. Nếu admin tăng giá cà phê ngày mai mà đơn hôm qua
//   tự đổi tổng tiền → báo cáo doanh thu sai. Nên dòng đơn chụp (snapshot) đơn giá khi tạo.
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

    // Đơn giá theo size, CHỐT tại thời điểm tạo dòng (đa hình qua Product.GetPrice)
    /// <summary>
    /// Đơn giá theo size lúc tạo dòng. Coffee/Tea cộng phụ thu, Cake trả giá gốc — OrderItem không cần biết loại.
    /// Buổi 32–41: lưu thành cột UnitPrice trong bảng OrderItems (giá lịch sử, không đổi khi menu đổi giá).
    /// </summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>Thành tiền của dòng = đơn giá × số lượng.</summary>
    public decimal TotalPrice => this.UnitPrice * this.Quantity;

    /// <summary>Tạo dòng mới; size bị bỏ qua (về S) nếu món không có size.</summary>
    public OrderItem(Product product, DrinkSize size, int quantity)
    {
        this.Product = product ?? throw new ArgumentNullException(nameof(product));
        this.Size = product.HasSize ? size : DrinkSize.S;
        this.Quantity = quantity;
        this.UnitPrice = product.GetPrice(this.Size);
    }

    // 👉 Bước 4 (b40.md): constructor rỗng PRIVATE chỉ dành cho EF Core.
    // EF đọc 1 dòng từ bảng OrderItems → cần tạo object trước rồi mới gán cột vào field.
    // Constructor public ở trên nhận Product (navigation) — EF không truyền navigation qua constructor được.
    // private: code của ta không gọi nhầm được (object rỗng không hợp lệ), EF vẫn gọi được bằng reflection.
    // ⚠️ Lỗi hay gặp: thiếu constructor này → "No suitable constructor was found for entity type 'OrderItem'".
    private OrderItem()
    {
        this.Product = null!; // EF gán lại khi Include(i => i.Product)
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
