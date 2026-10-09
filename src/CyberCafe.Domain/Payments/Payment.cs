// ============================================================================
// Payment.cs — abstract class cha của các hình thức thanh toán
// (Buổi 24–31 · OOP: Abstraction + Polymorphism).
// ============================================================================
namespace CyberCafe.Domain.Payments;

/// <summary>
/// Class cha cho các phương thức thanh toán (port từ day-04).
/// Polymorphism: Cash/Card/Momo override Process().
/// Thêm hình thức mới = thêm class mới, không sửa Order.Checkout().
/// Khác day-04: abstract — không ai tạo "Payment chung chung".
/// </summary>
public abstract class Payment
{
    private decimal _amount;

    /// <summary>Số tiền cần thanh toán (không âm). Order.Checkout gán lại bằng FinalAmount.</summary>
    public decimal Amount
    {
        get => this._amount;
        set
        {
            if (value < 0)
            {
                throw new ArgumentException("Số tiền thanh toán không hợp lệ");
            }

            this._amount = value;
        }
    }

    /// <summary>Đã thanh toán xong chưa. <c>protected set</c>: chỉ class này và class con được đổi.</summary>
    public bool IsPaid { get; protected set; }

    /// <summary>
    /// Loại thanh toán. <c>abstract</c> = không có thân hàm, class con BẮT BUỘC override
    /// (quên override → lỗi biên dịch).
    /// </summary>
    public abstract PaymentMethod Method { get; }

    /// <summary>Constructor <c>protected</c>: chỉ class con gọi được qua <c>: base(amount)</c>.</summary>
    protected Payment(decimal amount)
    {
        this.Amount = amount;
    }

    /// <summary>
    /// Xử lý thanh toán, trả về thông điệp kết quả. <c>virtual</c> (có sẵn bản mặc định)
    /// — khác <c>abstract</c> ở chỗ class con được phép override nhưng không bắt buộc.
    /// </summary>
    public virtual string Process()
    {
        this.IsPaid = true;
        return $"Thanh toán {this.Amount:N0} đ thành công";
    }

    /// <summary>Chuỗi mô tả số tiền + trạng thái.</summary>
    public virtual string Display()
    {
        string status = this.IsPaid ? "Đã thanh toán" : "Chưa thanh toán";
        return $"{this.Amount:N0} đ - {status}";
    }
}
