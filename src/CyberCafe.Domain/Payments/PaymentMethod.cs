// ============================================================================
// PaymentMethod.cs — enum hình thức thanh toán (Buổi 24–31).
// Dùng ở form Checkout (InputSelect) và trong property Payment.Method.
// ============================================================================
namespace CyberCafe.Domain.Payments;

/// <summary>Hình thức thanh toán chọn trên form checkout.</summary>
public enum PaymentMethod
{
    /// <summary>Tiền mặt tại quầy.</summary>
    Cash,

    /// <summary>Thẻ ngân hàng.</summary>
    Card,

    /// <summary>Ví MoMo theo số điện thoại.</summary>
    Momo
}
