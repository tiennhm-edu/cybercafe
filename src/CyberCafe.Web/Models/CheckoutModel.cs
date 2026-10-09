// ============================================================================
// CheckoutModel.cs — model cho form đặt hàng (Buổi 24–31 · EditForm + DataAnnotations).
// Mỗi attribute [Required], [StringLength], [RegularExpression]... là 1 quy tắc kiểm tra.
// <DataAnnotationsValidator /> trong Checkout.razor đọc các attribute này, hiển thị lỗi
// ngay trên form và chỉ gọi OnValidSubmit khi mọi quy tắc đều đạt.
// ============================================================================
using System.ComponentModel.DataAnnotations;
using CyberCafe.Domain.Payments;

namespace CyberCafe.Web.Models;

/// <summary>
/// View model cho form Checkout (EditForm + DataAnnotationsValidator).
/// Tách khỏi domain: form có thể "sai tạm thời" khi người dùng đang gõ,
/// còn domain object thì luôn hợp lệ (validate trong property setter).
/// </summary>
public class CheckoutModel : IValidatableObject
{
    /// <summary>Họ tên khách: bắt buộc, 2–50 ký tự.</summary>
    [Required(ErrorMessage = "Vui lòng nhập họ tên")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "Họ tên từ 2 đến 50 ký tự")]
    [Display(Name = "Họ tên")]
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// SĐT: regex <c>^0\d{9,10}$</c> = bắt đầu bằng 0, theo sau 9–10 chữ số (tổng 10–11),
    /// khớp với quy tắc Person.IsValidPhoneNumber ở domain.
    /// </summary>
    [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
    [RegularExpression(@"^0\d{9,10}$", ErrorMessage = "Số điện thoại gồm 10–11 chữ số, bắt đầu bằng 0")]
    [Display(Name = "Số điện thoại")]
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>
    /// Hình thức thanh toán. Kiểu nullable <c>PaymentMethod?</c> để [Required] có ý nghĩa
    /// (enum thường luôn có giá trị, [Required] sẽ không bao giờ báo lỗi).
    /// Viết đầy đủ <c>Domain.Payments.PaymentMethod.Cash</c> vì tên property trùng tên kiểu enum.
    /// </summary>
    [Required(ErrorMessage = "Vui lòng chọn hình thức thanh toán")]
    [Display(Name = "Thanh toán")]
    public PaymentMethod? PaymentMethod { get; set; } = Domain.Payments.PaymentMethod.Cash;

    // Chỉ bắt buộc khi chọn thẻ — validate chéo trong Validate()
    /// <summary>Số thẻ — chỉ kiểm tra khi chọn thanh toán thẻ (xem <see cref="Validate"/>).</summary>
    [Display(Name = "Số thẻ")]
    public string? CardNumber { get; set; }

    /// <summary>Ghi chú tùy chọn, tối đa 200 ký tự.</summary>
    [StringLength(200, ErrorMessage = "Ghi chú tối đa 200 ký tự")]
    [Display(Name = "Ghi chú")]
    public string? Note { get; set; }

    /// <summary>
    /// IValidatableObject: quy tắc phụ thuộc NHIỀU field (attribute chỉ nhìn được 1 field).
    /// Chạy SAU khi mọi attribute đã hợp lệ. <c>yield return</c> trả về từng lỗi;
    /// tham số thứ 2 (tên field) giúp lỗi hiện đúng dưới ô CardNumber.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (PaymentMethod == Domain.Payments.PaymentMethod.Card
            && (string.IsNullOrWhiteSpace(CardNumber) || CardNumber.Length < 12 || !CardNumber.All(char.IsDigit)))
        {
            yield return new ValidationResult("Số thẻ gồm ít nhất 12 chữ số", [nameof(CardNumber)]);
        }
    }

    // Tạo Payment cụ thể theo lựa chọn — phần còn lại của app chỉ thấy Payment (class cha)
    /// <summary>
    /// "Factory method": chuyển lựa chọn trên form thành object Payment cụ thể
    /// (CashPayment / CardPayment / MomoPayment). Nơi gọi chỉ cần biết kiểu cha Payment.
    /// </summary>
    public Payment CreatePayment(decimal amount) => PaymentMethod switch
    {
        Domain.Payments.PaymentMethod.Card => new CardPayment(amount, CardNumber!, "Visa/Master"),
        Domain.Payments.PaymentMethod.Momo => new MomoPayment(amount, PhoneNumber),
        _ => new CashPayment(amount, amount) // trả tại quầy, khách đưa đủ
    };
}
