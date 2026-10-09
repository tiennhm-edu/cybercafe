// ============================================================================
// CheckoutModel.cs — model cho form đặt hàng (Buổi 24–31 · EditForm + DataAnnotations).
// Mỗi attribute [Required], [StringLength], [RegularExpression]... là 1 quy tắc kiểm tra.
// <DataAnnotationsValidator /> trong Checkout.razor đọc các attribute này, hiển thị lỗi
// ngay trên form và chỉ gọi OnValidSubmit khi mọi quy tắc đều đạt.
// Buổi 32–41: form không tự tạo Payment/Order nữa mà chuyển thành PlaceOrderRequest gửi lên API
// (CreatePayment chuyển xuống Domain thành PaymentFactory, API gọi).
// ============================================================================
using System.ComponentModel.DataAnnotations;
using CyberCafe.Contracts.Orders;
using CyberCafe.Domain.Orders;
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

    // 👉 Bước 11 (b40.md): form → request. Chỉ gửi Id món + size + số lượng + MÃ giảm giá; KHÔNG gửi giá.
    /// <summary>
    /// Chuyển dữ liệu form + giỏ hàng thành body của POST /api/orders.
    /// Giá, tổng tiền, số tiền giảm do API tự tính lại từ database (không tin client).
    /// </summary>
    public PlaceOrderRequest ToRequest(Cart cart, string? discountCode) => new()
    {
        CustomerName = FullName.Trim(),
        PhoneNumber = PhoneNumber.Trim(),
        Items = cart.Items.Select(i => new OrderLineRequest(i.Product.Id, i.Size, i.Quantity)).ToList(),
        DiscountCode = discountCode,
        // PaymentMethod đã qua [Required] nên không null; ?? chỉ để compiler yên tâm
        PaymentMethod = PaymentMethod ?? Domain.Payments.PaymentMethod.Cash,
        CardNumber = PaymentMethod == Domain.Payments.PaymentMethod.Card ? CardNumber : null,
        Note = Note
    };
}
