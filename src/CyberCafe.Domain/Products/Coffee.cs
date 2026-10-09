// ============================================================================
// Coffee.cs — cà phê (Buổi 24–31 · Inheritance nhiều tầng: Coffee → Drink → Product).
// ============================================================================
namespace CyberCafe.Domain.Products;

/// <summary>
/// Coffee kế thừa Drink → Product, thêm BeanType (Robusta/Arabica...).
/// </summary>
public class Coffee : Drink
{
    private string _beanType = string.Empty;

    /// <summary>Loại hạt (Robusta, Arabica...), không được rỗng.</summary>
    public string BeanType
    {
        get => this._beanType;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Loại hạt không được để trống");
            }

            this._beanType = value;
        }
    }

    /// <summary>Nhóm "Cà phê" trên menu.</summary>
    public override string Category => "Cà phê";

    /// <summary>Tạo cà phê; tên + giá chuyển lên Drink → Product qua <c>base(...)</c>.</summary>
    public Coffee(string name, decimal price, string beanType) : base(name, price)
    {
        this.BeanType = beanType;
    }

    /// <summary>Cách pha riêng của cà phê.</summary>
    public override string Prepare()
    {
        return $"Pha cà phê {this.Name} ({this.BeanType})...";
    }

    /// <summary>Mô tả của Product + loại hạt.</summary>
    public override string Display()
    {
        return $"{base.Display()} - Hạt: {this.BeanType}";
    }
}
