// ============================================================================
// CardPayment.cs — thanh toán thẻ (Buổi 24–31 · Inheritance + Encapsulation).
// ============================================================================
namespace CyberCafe.Domain.Payments;

/// <summary>
/// Thanh toán thẻ: số thẻ (chỉ hiện 4 số cuối) + ngân hàng.
/// </summary>
public class CardPayment : Payment
{
    private string _cardNumber = string.Empty;

    /// <summary>Số thẻ đầy đủ (tối thiểu 4 ký tự). Chỉ dùng nội bộ; khi hiển thị dùng <see cref="MaskedCardNumber"/>.</summary>
    public string CardNumber
    {
        get => this._cardNumber;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length < 4)
            {
                throw new ArgumentException("Số thẻ không hợp lệ");
            }

            this._cardNumber = value;
        }
    }

    /// <summary>Tên ngân hàng phát hành.</summary>
    public string BankName { get; set; }

    // Encapsulation: che thông tin nhạy cảm
    /// <summary>Số thẻ đã che, vd "****1111". <c>[^4..]</c> là range operator: lấy 4 ký tự cuối.</summary>
    public string MaskedCardNumber => "****" + this._cardNumber[^4..];

    /// <summary>Luôn là <see cref="PaymentMethod.Card"/>.</summary>
    public override PaymentMethod Method => PaymentMethod.Card;

    /// <summary>Tạo thanh toán thẻ; tên ngân hàng rỗng thì dùng mặc định "Ngân hàng".</summary>
    public CardPayment(decimal amount, string cardNumber, string bankName) : base(amount)
    {
        this.CardNumber = cardNumber;
        this.BankName = string.IsNullOrWhiteSpace(bankName) ? "Ngân hàng" : bankName;
    }

    /// <summary>Giả lập trừ tiền thẻ (demo, không gọi cổng thanh toán thật).</summary>
    public override string Process()
    {
        this.IsPaid = true;
        return $"Thẻ {this.BankName} ({this.MaskedCardNumber}): thanh toán {this.Amount:N0} đ thành công";
    }

    /// <summary>Mô tả kèm ngân hàng và số thẻ đã che.</summary>
    public override string Display()
    {
        return $"Thẻ {this.BankName} | {this.MaskedCardNumber} | {base.Display()}";
    }
}
