// ============================================================================
// PaymentFactory.cs — tạo Payment cụ thể theo lựa chọn (Buổi 24–31 là CheckoutModel.CreatePayment;
//                    Buổi 32–41 chuyển xuống Domain vì giờ API mới là nơi tạo thanh toán).
// Factory method: nơi gọi đưa vào "loại" (enum) → nhận về object kiểu cha Payment.
// ============================================================================
namespace CyberCafe.Domain.Payments;

/// <summary>
/// Chuyển lựa chọn thanh toán (enum) thành object Payment cụ thể: Cash / Card / Momo.
/// </summary>
public static class PaymentFactory
{
    /// <summary>
    /// Tạo Payment. Thẻ: chỉ giữ 4 số cuối — đơn được lưu xuống DB, mà hệ thống bán hàng
    /// KHÔNG được lưu số thẻ đầy đủ (chuẩn PCI DSS). Demo không gọi cổng thanh toán thật.
    /// </summary>
    /// <exception cref="ArgumentException">Thiếu số thẻ khi chọn thẻ, hoặc SĐT MoMo sai.</exception>
    public static Payment Create(PaymentMethod method, decimal amount, string phoneNumber, string? cardNumber)
    {
        return method switch
        {
            PaymentMethod.Card => new CardPayment(amount, MaskCard(cardNumber), "Visa/Master"),
            PaymentMethod.Momo => new MomoPayment(amount, phoneNumber),
            _ => new CashPayment(amount, amount) // trả tại quầy, khách đưa đủ
        };
    }

    // "4111111111111111" → "****1111". CardPayment vẫn validate độ dài ≥ 4 như cũ.
    // ⚠️ Lỗi hay gặp: log/lưu nguyên request chứa số thẻ → lộ dữ liệu thẻ trong log và DB.
    private static string MaskCard(string? cardNumber)
    {
        if (string.IsNullOrWhiteSpace(cardNumber) || cardNumber.Length < 4)
        {
            throw new ArgumentException("Số thẻ không hợp lệ");
        }

        return "****" + cardNumber[^4..];
    }
}
