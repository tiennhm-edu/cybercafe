// ============================================================================
// Person.cs — class cha cho người (Buổi 24–31 · Inheritance + static method; Buổi 50: luật SĐT → PhoneNumber).
// ============================================================================
namespace CyberCafe.Domain.People;

/// <summary>
/// Class cha: thông tin chung của người (Customer, Employee kế thừa) — port từ day-04.
/// Khác day-04: SĐT chấp nhận 10–11 chữ số, bắt đầu bằng 0 (khớp form checkout).
/// </summary>
public class Person
{
    private string _fullName = string.Empty;

    /// <summary>Họ tên (đã Trim), không được rỗng.</summary>
    public string FullName
    {
        get => this._fullName;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Họ tên không được để trống");
            }

            this._fullName = value.Trim();
        }
    }

    private string _phoneNumber = string.Empty;

    /// <summary>Số điện thoại hợp lệ theo <see cref="IsValidPhoneNumber"/>.</summary>
    public string PhoneNumber
    {
        get => this._phoneNumber;
        set
        {
            if (!IsValidPhoneNumber(value))
            {
                throw new ArgumentException("Số điện thoại phải có 10–11 chữ số và bắt đầu bằng 0");
            }

            this._phoneNumber = value;
        }
    }

    /// <summary>Tạo người với họ tên và SĐT (cả hai đều được validate).</summary>
    public Person(string fullName, string phoneNumber)
    {
        this.FullName = fullName;
        this.PhoneNumber = phoneNumber;
    }

    // static: quy tắc dùng chung, không cần object (MomoPayment cũng gọi)
    /// <summary>Kiểm tra SĐT: 10–11 chữ số, bắt đầu bằng 0 (ủy quyền cho <see cref="Common.PhoneNumber.IsValid"/>).</summary>
    // Buổi 50: luật SĐT chuyển vào value object PhoneNumber (1 chỗ duy nhất); method này giữ lại cho code cũ gọi.
    public static bool IsValidPhoneNumber(string? value) => Common.PhoneNumber.IsValid(value); // "Common." vì trùng tên property PhoneNumber

    /// <summary>Chuỗi mô tả "Họ tên - SĐT"; class con override để thêm thông tin.</summary>
    public virtual string Display()
    {
        return $"{this.FullName} - {this.PhoneNumber}";
    }
}
