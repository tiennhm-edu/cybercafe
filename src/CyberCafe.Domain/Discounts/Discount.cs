// ============================================================================
// Discount.cs — class cha của các loại giảm giá (Buổi 24–31 · OOP: Polymorphism).
// Thuộc project CyberCafe.Domain: C# thuần, KHÔNG tham chiếu Blazor/ASP.NET.
// → test được bằng xUnit không cần web, và tái sử dụng được cho Web API (buổi 32+).
// ============================================================================
namespace CyberCafe.Domain.Discounts;

/// <summary>
/// Class cha cho các loại giảm giá (port từ day-04).
/// Polymorphism: MemberDiscount (%), VoucherDiscount (số tiền cố định) override Apply().
/// Cart/Order chỉ gọi discount.Apply(total) — không if theo loại.
/// </summary>
public class Discount
{
    private string _name = string.Empty;

    /// <summary>Tên hiển thị của chương trình giảm giá. Không được rỗng (validate trong setter).</summary>
    public string Name
    {
        get => this._name;
        set
        {
            // Encapsulation: không cho object rơi vào trạng thái sai — ném exception ngay khi gán sai
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Tên giảm giá không được để trống");
            }

            this._name = value;
        }
    }

    /// <summary>Tạo giảm giá với tên cho trước. Gán qua property để tận dụng validate.</summary>
    public Discount(string name)
    {
        this.Name = name;
    }

    // Trả về số tiền SAU khi giảm
    /// <summary>
    /// Áp dụng giảm giá lên <paramref name="originalAmount"/>, trả về số tiền SAU khi giảm.
    /// <c>virtual</c>: class con override để đưa ra quy tắc riêng; bản gốc không giảm gì.
    /// </summary>
    public virtual decimal Apply(decimal originalAmount)
    {
        EnsureValidAmount(originalAmount);
        return originalAmount;
    }

    // Số tiền được giảm
    /// <summary>
    /// Số tiền được giảm = gốc − sau giảm. Không cần override: nó gọi <see cref="Apply"/>,
    /// mà Apply là virtual → tự chạy đúng phiên bản của class con (Template Method đơn giản).
    /// </summary>
    public decimal GetDiscountAmount(decimal originalAmount)
    {
        return originalAmount - this.Apply(originalAmount);
    }

    /// <summary>Chuỗi mô tả để hiện trên giao diện (class con bổ sung chi tiết).</summary>
    public virtual string Display()
    {
        return this.Name;
    }

    // protected static: chỉ class này và class con dùng được; static vì không cần dữ liệu của object
    /// <summary>Kiểm tra số tiền gốc không âm — dùng chung cho mọi class con.</summary>
    protected static void EnsureValidAmount(decimal amount)
    {
        if (amount < 0)
        {
            throw new ArgumentException("Số tiền gốc không hợp lệ");
        }
    }
}
