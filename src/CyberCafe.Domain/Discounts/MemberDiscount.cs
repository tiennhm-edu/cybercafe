// ============================================================================
// MemberDiscount.cs — giảm theo % (Buổi 24–31 · OOP: Inheritance + override).
// ============================================================================
namespace CyberCafe.Domain.Discounts;

/// <summary>
/// Giảm giá thành viên theo phần trăm (vd: member 10%).
/// </summary>
public class MemberDiscount : Discount
{
    private decimal _percent;

    // Phần trăm giảm (0–100)
    /// <summary>Phần trăm giảm, hợp lệ trong khoảng 0–100.</summary>
    public decimal Percent
    {
        get => this._percent;
        set
        {
            if (value < 0 || value > 100)
            {
                throw new ArgumentException("Phần trăm giảm giá phải từ 0 đến 100");
            }

            this._percent = value;
        }
    }

    /// <summary>Tạo giảm giá thành viên. <c>: base(name)</c> gọi constructor của class cha Discount trước.</summary>
    public MemberDiscount(string name, decimal percent) : base(name)
    {
        this.Percent = percent;
    }

    /// <summary>Trả về số tiền sau khi giảm <see cref="Percent"/>% (override quy tắc của class cha).</summary>
    public override decimal Apply(decimal originalAmount)
    {
        EnsureValidAmount(originalAmount);
        decimal discountAmount = originalAmount * this.Percent / 100;
        return originalAmount - discountAmount;
    }

    /// <summary>Mô tả kèm %; <c>base.Display()</c> tái sử dụng phần tên từ class cha.</summary>
    public override string Display()
    {
        return $"{base.Display()} - Giảm {this.Percent}%";
    }
}
