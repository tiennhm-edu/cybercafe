// ============================================================================
// VoucherDiscount.cs — giảm số tiền cố định theo mã (Buổi 24–31 · OOP: override).
// ============================================================================
namespace CyberCafe.Domain.Discounts;

/// <summary>
/// Giảm giá bằng voucher: mã + số tiền cố định. Không cho tổng âm.
/// </summary>
public class VoucherDiscount : Discount
{
    private string _code = string.Empty;

    /// <summary>Mã voucher. Setter chuẩn hóa: bỏ khoảng trắng, viết HOA → "giam20k " thành "GIAM20K".</summary>
    public string Code
    {
        get => this._code;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Mã voucher không được để trống");
            }

            this._code = value.Trim().ToUpperInvariant();
        }
    }

    private decimal _amount;

    // Số tiền giảm cố định (VND)
    /// <summary>Số tiền giảm cố định (VND), phải &gt; 0.</summary>
    public decimal Amount
    {
        get => this._amount;
        set
        {
            if (value <= 0)
            {
                throw new ArgumentException("Số tiền giảm phải lớn hơn 0");
            }

            this._amount = value;
        }
    }

    /// <summary>Tạo voucher với tên, mã và số tiền giảm.</summary>
    public VoucherDiscount(string name, string code, decimal amount) : base(name)
    {
        this.Code = code;
        this.Amount = amount;
    }

    /// <summary>Trừ thẳng <see cref="Amount"/>; đơn nhỏ hơn giá trị voucher thì về 0 (không âm).</summary>
    public override decimal Apply(decimal originalAmount)
    {
        EnsureValidAmount(originalAmount);
        decimal result = originalAmount - this.Amount;
        return result < 0 ? 0 : result;
    }

    /// <summary>Mô tả kèm mã và số tiền giảm.</summary>
    public override string Display()
    {
        return $"{base.Display()} - Mã: {this.Code} (-{this.Amount:N0} đ)";
    }
}
