// ============================================================================
// Employee.cs — nhân viên (Buổi 24–31 · Inheritance). Chưa dùng trên UI.
// ============================================================================
namespace CyberCafe.Domain.People;

/// <summary>
/// Employee kế thừa Person, thêm Position (Barista, Thu ngân, Quản lý...).
/// Sẽ dùng ở buổi 33–34 (màn hình quầy barista) và 42–47 (phân quyền).
/// </summary>
public class Employee : Person
{
    private string _position = string.Empty;

    /// <summary>Chức vụ, không được rỗng.</summary>
    public string Position
    {
        get => this._position;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Chức vụ không được để trống");
            }

            this._position = value;
        }
    }

    /// <summary>Tạo nhân viên với họ tên, SĐT và chức vụ.</summary>
    public Employee(string fullName, string phoneNumber, string position) : base(fullName, phoneNumber)
    {
        this.Position = position;
    }

    /// <summary>Mô tả của Person + chức vụ.</summary>
    public override string Display()
    {
        return $"{base.Display()} | Chức vụ: {this.Position}";
    }
}
