// ============================================================================
// DiscountService.cs — tra mã giảm giá (Buổi 24–31 · service + polymorphism).
// Đăng ký Singleton (không giữ state riêng của người dùng nào).
// ============================================================================
using CyberCafe.Domain.Discounts;

namespace CyberCafe.Web.Services;

/// <summary>
/// Tra mã giảm giá. Trả về Discount (class cha) — trang Cart không cần biết là loại nào.
/// </summary>
public class DiscountService
{
    /// <summary>Các mã mẫu để demo / gợi ý trên giao diện.</summary>
    public static readonly IReadOnlyList<string> SampleCodes = ["GIAM20K", "MEMBER10"];

    /// <summary>
    /// Tìm giảm giá theo mã (bỏ khoảng trắng, không phân biệt hoa/thường).
    /// Trả về object mới mỗi lần, hoặc null nếu mã không tồn tại.
    /// </summary>
    public Discount? FindByCode(string? code)
    {
        string normalized = (code ?? string.Empty).Trim().ToUpperInvariant();

        // switch expression: mỗi nhánh tạo 1 loại Discount khác nhau, kiểu trả về chung là Discount
        return normalized switch
        {
            "GIAM20K" => new VoucherDiscount("Voucher 20K", "GIAM20K", 20000),
            "MEMBER10" => new MemberDiscount("Thành viên CyberCafe", 10),
            _ => null
        };
    }
}
