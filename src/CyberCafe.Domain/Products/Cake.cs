// ============================================================================
// Cake.cs — bánh (Buổi 24–31 · Inheritance trực tiếp từ Product, không có size).
// Không override GetPrice/HasSize → dùng bản mặc định của Product (giá cố định, không size).
// ============================================================================
namespace CyberCafe.Domain.Products;

/// <summary>
/// Cake kế thừa Product (không có size), thêm Flavor (hương vị).
/// </summary>
public class Cake : Product
{
    private string _flavor = string.Empty;

    /// <summary>Hương vị bánh, không được rỗng.</summary>
    public string Flavor
    {
        get => this._flavor;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Hương vị không được để trống");
            }

            this._flavor = value;
        }
    }

    /// <summary>Nhóm "Bánh" trên menu.</summary>
    public override string Category => "Bánh";

    /// <summary>Tạo bánh với tên, giá và hương vị.</summary>
    public Cake(string name, decimal price, string flavor) : base(name, price)
    {
        this.Flavor = flavor;
    }

    /// <summary>Cách chuẩn bị riêng của bánh.</summary>
    public override string Prepare()
    {
        return $"Cắt bánh {this.Name} vị {this.Flavor}...";
    }

    /// <summary>Mô tả của Product + hương vị.</summary>
    public override string Display()
    {
        return $"{base.Display()} - Vị: {this.Flavor}";
    }
}
