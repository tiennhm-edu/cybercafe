// ============================================================================
// PhoneNumber.cs — VALUE OBJECT số điện thoại (Buổi 50 · value object).
// Trước b53, luật "10–11 chữ số, bắt đầu bằng 0" nằm ở Person.IsValidPhoneNumber (static) và được gọi từ
// Person, MomoPayment... Giờ luật nằm ở ĐÚNG 1 chỗ: PhoneNumber.IsValid; ai cần SĐT "chắc chắn hợp lệ"
// thì cầm PhoneNumber (đã tạo được = đã hợp lệ) thay vì string (có thể là bất cứ gì).
// Lưu DB: vẫn là chuỗi trong cột Orders.CustomerPhone (Customer giữ .Value) — value object KHÔNG bắt buộc
// phải là kiểu riêng trong database; nó là kiểu riêng trong CODE để luật không bị chép rải rác.
// ============================================================================
namespace CyberCafe.Domain.Common;

// 👉 Bước 4 (b50.md)
/// <summary>Số điện thoại Việt Nam đã chuẩn hóa (bỏ khoảng trắng), luôn hợp lệ.</summary>
public sealed record PhoneNumber
{
    // Constructor private: cách DUY NHẤT để có PhoneNumber là đi qua Create (có validate)
    private PhoneNumber(string value) => this.Value = value;

    /// <summary>Chuỗi chữ số, vd "0901234567".</summary>
    public string Value { get; }

    /// <summary>Tạo từ chuỗi người dùng nhập (cho phép khoảng trắng 2 đầu).</summary>
    /// <exception cref="ArgumentException">Sai định dạng (→ 400).</exception>
    public static PhoneNumber Create(string? value)
    {
        string normalized = (value ?? string.Empty).Trim();
        if (!IsValid(normalized))
        {
            throw new ArgumentException("Số điện thoại phải có 10–11 chữ số và bắt đầu bằng 0");
        }

        return new PhoneNumber(normalized);
    }

    /// <summary>
    /// Luật SĐT: 10–11 chữ số, bắt đầu bằng 0.
    /// <c>is &gt;= 10 and &lt;= 11</c> là pattern matching (C# 9) — gọn hơn viết 2 phép so sánh.
    /// </summary>
    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.StartsWith('0')
        && value.Length is >= 10 and <= 11
        && value.All(char.IsDigit);

    /// <summary>Trả về chuỗi số (để ghép chuỗi / lưu DB).</summary>
    public override string ToString() => this.Value;
}
