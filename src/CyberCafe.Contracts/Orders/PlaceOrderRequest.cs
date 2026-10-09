// ============================================================================
// PlaceOrderRequest.cs — body của POST /api/orders (Buổi 32–41 · đặt hàng qua API).
// Client chỉ gửi "ý định": món nào, size nào, bao nhiêu, mã giảm giá nào.
// Client KHÔNG gửi giá / tổng tiền / số tiền giảm — server tự tính từ database + DiscountCatalog.
// ⚠️ Lỗi hay gặp: nhận "price" từ client rồi lưu thẳng → ai cũng tự đặt giá 1đ được.
// ============================================================================
using System.ComponentModel.DataAnnotations;
using CyberCafe.Domain.Payments;
using CyberCafe.Domain.Products;

namespace CyberCafe.Contracts.Orders;

/// <summary>1 dòng món trong yêu cầu đặt hàng.</summary>
/// <param name="ProductId">Mã món trên thực đơn.</param>
/// <param name="Size">Size (bánh luôn được tính là S).</param>
/// <param name="Quantity">Số lượng 1–20.</param>
// Attribute validate đặt trên THAM SỐ của primary constructor (MVC đọc từ đó khi bind record).
// ⚠️ Lỗi hay gặp: viết [property: Range(...)] → ASP.NET Core ném InvalidOperationException
//    "validation metadata ... must be associated with the constructor parameter" ngay khi bind request.
public record OrderLineRequest(
    [Range(1, int.MaxValue, ErrorMessage = "Mã món không hợp lệ")] int ProductId,
    DrinkSize Size,
    [Range(1, 20, ErrorMessage = "Số lượng từ 1 đến 20")] int Quantity);

/// <summary>Yêu cầu đặt hàng. Quy tắc giống CheckoutModel ở Web — server kiểm tra lại lần nữa.</summary>
public class PlaceOrderRequest : IValidatableObject
{
    /// <summary>Họ tên khách, 2–50 ký tự.</summary>
    [Required(ErrorMessage = "Vui lòng nhập họ tên")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "Họ tên từ 2 đến 50 ký tự")]
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>SĐT 10–11 chữ số, bắt đầu bằng 0.</summary>
    [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
    [RegularExpression(@"^0\d{9,10}$", ErrorMessage = "Số điện thoại gồm 10–11 chữ số, bắt đầu bằng 0")]
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>Các dòng món — ít nhất 1 dòng.</summary>
    [MinLength(1, ErrorMessage = "Đơn hàng phải có ít nhất 1 món")]
    public List<OrderLineRequest> Items { get; set; } = [];

    /// <summary>Mã giảm giá (tùy chọn) — server tự tra lại.</summary>
    [StringLength(20)]
    public string? DiscountCode { get; set; }

    /// <summary>Hình thức thanh toán.</summary>
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    /// <summary>Số thẻ — chỉ cần khi PaymentMethod = Card. Server chỉ lưu 4 số cuối.</summary>
    public string? CardNumber { get; set; }

    /// <summary>Ghi chú, tối đa 200 ký tự.</summary>
    [StringLength(200, ErrorMessage = "Ghi chú tối đa 200 ký tự")]
    public string? Note { get; set; }

    /// <summary>Quy tắc chéo nhiều field: chọn thẻ thì phải có số thẻ ≥ 12 chữ số.</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (this.PaymentMethod == PaymentMethod.Card
            && (string.IsNullOrWhiteSpace(this.CardNumber) || this.CardNumber.Length < 12 || !this.CardNumber.All(char.IsDigit)))
        {
            yield return new ValidationResult("Số thẻ gồm ít nhất 12 chữ số", [nameof(this.CardNumber)]);
        }
    }
}
