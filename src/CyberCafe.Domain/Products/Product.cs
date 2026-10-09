// ============================================================================
// Product.cs — class gốc của mọi món (Buổi 24–31 · 4 tính chất OOP).
// Cây kế thừa:
//   Product ─┬─ Drink (abstract, có size) ─┬─ Coffee
//            │                              └─ Tea
//            └─ Cake (không size)
// Lưu ý: Product KHÔNG đánh dấu abstract (giữ như bài day-04) nhưng trên menu chỉ
// tạo Coffee/Tea/Cake. Drink thì abstract vì "đồ uống chung chung" không có thật.
// Vì sao domain là C# thuần, không dính Blazor? Xem ghi chú đầu file Cart.cs.
// ============================================================================
namespace CyberCafe.Domain.Products;

/// <summary>
/// Class cha của sản phẩm (Coffee, Tea, Cake) — port từ day-04 CoffeeShopManagement.
/// OOP:
/// - Encapsulation: Name/Price validate trong property.
/// - Inheritance: Coffee/Tea/Cake kế thừa Id, Name, Price.
/// - Polymorphism: GetPrice(size) / Prepare() / Display() mỗi loại override khác nhau.
/// </summary>
public class Product
{
    #region Properties
    /// <summary>Mã món (gán qua object initializer khi seed menu).</summary>
    public int Id { get; set; }

    private string _name = string.Empty;

    /// <summary>Tên món, không được rỗng.</summary>
    public string Name
    {
        get => this._name;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Tên không được để trống");
            }

            this._name = value;
        }
    }

    private decimal _price;

    // Giá cơ bản (size S hoặc món không có size)
    /// <summary>Giá cơ bản (VND). <c>private set</c>: muốn đổi giá phải qua <see cref="UpdatePrice"/>.</summary>
    public decimal Price
    {
        get => this._price;
        private set
        {
            if (value < 0)
            {
                throw new ArgumentException("Giá tiền không hợp lệ");
            }

            this._price = value;
        }
    }

    /// <summary>Mô tả ngắn hiển thị trên card.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Emoji minh họa thay ảnh.</summary>
    public string Emoji { get; set; } = "🍽️";

    /// <summary><c>false</c> = tạm hết, không thêm vào giỏ được.</summary>
    public bool IsAvailable { get; set; } = true;

    // Nhóm hiển thị trên menu — class con override
    /// <summary>Nhóm món; <c>virtual</c> property — class con trả về "Cà phê", "Trà", "Bánh".</summary>
    public virtual string Category => "Khác";

    // Món có chọn size không? Đồ uống: có; bánh: không
    /// <summary>Món có chọn size không. ProductCard dựa vào đây để hiện/ẩn ô chọn size.</summary>
    public virtual bool HasSize => false;
    #endregion Properties

    #region Constructor
    /// <summary>Tạo món với tên và giá cơ bản (cả hai được validate).</summary>
    public Product(string name, decimal price)
    {
        this.Name = name;
        this.Price = price;
    }
    #endregion Constructor

    #region Methods
    /// <summary>Đổi giá cơ bản (vẫn đi qua validate của setter).</summary>
    public void UpdatePrice(decimal newPrice)
    {
        this.Price = newPrice;
    }

    // virtual: giá theo size — mặc định không phụ thu
    /// <summary>
    /// Giá theo size — điểm mấu chốt của POLYMORPHISM: OrderItem chỉ gọi
    /// <c>Product.GetPrice(size)</c>; object thật là Coffee/Tea thì chạy bản override
    /// trong Drink (có phụ thu), là Cake thì chạy bản này (bỏ qua size).
    /// </summary>
    public virtual decimal GetPrice(DrinkSize size)
    {
        return this.Price;
    }

    /// <summary>Mô tả cách chuẩn bị món (mỗi loại override).</summary>
    public virtual string Prepare()
    {
        return $"Đang chuẩn bị {this.Name}...";
    }

    /// <summary>Chuỗi mô tả "Tên | Giá".</summary>
    public virtual string Display()
    {
        return $"{this.Name} | Giá: {this.Price:N0} đ";
    }
    #endregion Methods
}
