// ============================================================================
// Tea.cs — trà (Buổi 24–31 · Inheritance: Tea → Drink → Product).
// Cấu trúc giống Coffee; giá theo size được thừa hưởng từ Drink.
// ============================================================================
namespace CyberCafe.Domain.Products;

/// <summary>
/// Tea kế thừa Drink → Product, thêm TeaType (trà đen, ô long...).
/// </summary>
public class Tea : Drink
{
    private string _teaType = string.Empty;

    /// <summary>Loại trà (trà đen, ô long, matcha...), không được rỗng.</summary>
    public string TeaType
    {
        get => this._teaType;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Loại trà không được để trống");
            }

            this._teaType = value;
        }
    }

    /// <summary>Nhóm "Trà" trên menu.</summary>
    public override string Category => "Trà";

    /// <summary>Tạo món trà với tên, giá cơ bản và loại trà.</summary>
    public Tea(string name, decimal price, string teaType) : base(name, price)
    {
        this.TeaType = teaType;
    }

    /// <summary>Cách pha riêng của trà.</summary>
    public override string Prepare()
    {
        return $"Pha trà {this.Name} ({this.TeaType})...";
    }

    /// <summary>Mô tả của Product + loại trà.</summary>
    public override string Display()
    {
        return $"{base.Display()} - Loại: {this.TeaType}";
    }
}
