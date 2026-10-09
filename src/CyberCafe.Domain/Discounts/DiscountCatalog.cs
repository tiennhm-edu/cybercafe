// ============================================================================
// DiscountCatalog.cs — tra mã giảm giá (Buổi 24–31 là Web/Services/DiscountService;
//                     Buổi 32–41 chuyển xuống Domain).
// Vì sao chuyển xuống Domain?
//   - Web dùng để XEM TRƯỚC số tiền giảm trên trang giỏ hàng.
//   - Api dùng để TÍNH THẬT khi đặt đơn. Không bao giờ tin số tiền giảm do client gửi lên:
//     client chỉ gửi MÃ, server tự tra lại → người dùng sửa request cũng không tự giảm 100% được.
//   Cùng 1 bảng mã cho cả 2 phía → không lệch nhau.
// ============================================================================
namespace CyberCafe.Domain.Discounts;

/// <summary>
/// Danh mục mã giảm giá (in-memory). Trả về Discount (class cha) — nơi gọi không cần biết là loại nào.
/// Static vì không giữ state và không phụ thuộc gì (buổi 48 có thể chuyển thành bảng trong DB).
/// </summary>
public static class DiscountCatalog
{
    /// <summary>Các mã mẫu để demo / gợi ý trên giao diện.</summary>
    public static readonly IReadOnlyList<string> SampleCodes = ["GIAM20K", "MEMBER10"];

    /// <summary>Chuẩn hóa mã: bỏ khoảng trắng, viết HOA (" giam20k " → "GIAM20K").</summary>
    public static string Normalize(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>
    /// Tìm giảm giá theo mã (không phân biệt hoa/thường). Trả về object MỚI mỗi lần
    /// (mỗi đơn giữ bản riêng khi lưu DB), hoặc null nếu mã không tồn tại.
    /// </summary>
    public static Discount? FindByCode(string? code)
    {
        // switch expression: mỗi nhánh tạo 1 loại Discount khác nhau, kiểu trả về chung là Discount
        return Normalize(code) switch
        {
            "GIAM20K" => new VoucherDiscount("Voucher 20K", "GIAM20K", 20000),
            "MEMBER10" => new MemberDiscount("Thành viên CyberCafe", 10),
            _ => null
        };
    }
}
