// ============================================================================
// MomoPayment.cs — thanh toán ví MoMo (Buổi 24–31 · Inheritance, tái dùng quy tắc static).
// ============================================================================
using CyberCafe.Domain.People;

namespace CyberCafe.Domain.Payments;

/// <summary>
/// Thanh toán ví MoMo theo số điện thoại.
/// </summary>
public class MomoPayment : Payment
{
    private string _phoneNumber = string.Empty;

    /// <summary>SĐT ví MoMo — dùng chung quy tắc kiểm tra với <see cref="Person.IsValidPhoneNumber"/>.</summary>
    public string PhoneNumber
    {
        get => this._phoneNumber;
        set
        {
            if (!Person.IsValidPhoneNumber(value))
            {
                throw new ArgumentException("Số điện thoại MoMo không hợp lệ");
            }

            this._phoneNumber = value;
        }
    }

    /// <summary>Luôn là <see cref="PaymentMethod.Momo"/>.</summary>
    public override PaymentMethod Method => PaymentMethod.Momo;

    /// <summary>Tạo thanh toán MoMo với số tiền và SĐT ví.</summary>
    public MomoPayment(decimal amount, string phoneNumber) : base(amount)
    {
        this.PhoneNumber = phoneNumber;
    }

    /// <summary>Giả lập thanh toán qua ví (demo).</summary>
    public override string Process()
    {
        this.IsPaid = true;
        return $"MoMo ({this.PhoneNumber}): thanh toán {this.Amount:N0} đ thành công";
    }

    /// <summary>Mô tả kèm SĐT ví.</summary>
    public override string Display()
    {
        return $"MoMo | SĐT: {this.PhoneNumber} | {base.Display()}";
    }
}
