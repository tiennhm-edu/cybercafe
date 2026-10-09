// ============================================================================
// CashPayment.cs — thanh toán tiền mặt (Buổi 24–31 · Inheritance + override).
// ============================================================================
namespace CyberCafe.Domain.Payments;

/// <summary>
/// Thanh toán tiền mặt: số tiền khách đưa + tiền thừa.
/// </summary>
public class CashPayment : Payment
{
    private decimal _cashReceived;

    /// <summary>Số tiền khách đưa (không âm).</summary>
    public decimal CashReceived
    {
        get => this._cashReceived;
        set
        {
            if (value < 0)
            {
                throw new ArgumentException("Số tiền khách đưa không hợp lệ");
            }

            this._cashReceived = value;
        }
    }

    /// <summary>Tiền thừa trả khách (không âm). Property chỉ có get, tính từ dữ liệu khác.</summary>
    public decimal Change
    {
        get
        {
            decimal change = this.CashReceived - this.Amount;
            return change < 0 ? 0 : change;
        }
    }

    /// <summary>Luôn là <see cref="PaymentMethod.Cash"/> (override property abstract của class cha).</summary>
    public override PaymentMethod Method => PaymentMethod.Cash;

    /// <summary>Tạo thanh toán tiền mặt với số tiền cần trả và số tiền khách đưa.</summary>
    public CashPayment(decimal amount, decimal cashReceived) : base(amount)
    {
        this.CashReceived = cashReceived;
    }

    /// <summary>Thu tiền; ném <see cref="InvalidOperationException"/> nếu khách đưa thiếu.</summary>
    public override string Process()
    {
        if (this.CashReceived < this.Amount)
        {
            throw new InvalidOperationException("Số tiền khách đưa không đủ");
        }

        this.IsPaid = true;
        return $"Tiền mặt: nhận {this.CashReceived:N0} đ, thừa {this.Change:N0} đ";
    }

    /// <summary>Mô tả kèm tiền tố "Tiền mặt".</summary>
    public override string Display()
    {
        return $"Tiền mặt | {base.Display()}";
    }
}
