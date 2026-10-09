// ============================================================================
// Money.cs — VALUE OBJECT số tiền (Buổi 50 · value object).
// Vì sao không dùng thẳng decimal?
//   - decimal cho phép -5000 → "đơn giá âm" lọt vào hệ thống. Money KHÔNG BAO GIỜ âm (kiểm tra trong constructor).
//   - Ý nghĩa rõ ràng: UnitPrice là Money, Quantity là int → không nhầm "cộng tiền với số lượng".
// Đặc điểm value object:
//   1) So sánh bằng GIÁ TRỊ: new Money(1000) == new Money(1000) là true (record struct tự sinh Equals).
//   2) BẤT BIẾN: không có setter; "đổi" = tạo object mới (a + b trả Money mới).
//   3) Tự validate: đã tạo được thì luôn hợp lệ.
// Quán chỉ dùng VND nên không có trường Currency (YAGNI). Thêm ngoại tệ thì thêm Currency + cấm cộng khác loại tiền.
// Lưu DB: Infrastructure dùng VALUE CONVERTER (Money ⇄ decimal) → cột OrderItems.UnitPrice không đổi kiểu.
// ============================================================================
using System.Globalization;

namespace CyberCafe.Domain.Common;

// 👉 Bước 1 (b50.md)
/// <summary>Số tiền VND, không âm, bất biến, so sánh theo giá trị.</summary>
public readonly record struct Money : IComparable<Money>, IFormattable
{
    /// <summary>0 đ.</summary>
    public static readonly Money Zero = new(0);

    /// <summary>Tạo số tiền; âm → ArgumentException (→ 400).</summary>
    public Money(decimal amount)
    {
        if (amount < 0)
        {
            throw new ArgumentException("Số tiền không được âm");
        }

        this.Amount = amount;
    }

    /// <summary>Giá trị decimal (để lưu DB, trả JSON, đưa cho Payment/Discount của buổi OOP).</summary>
    public decimal Amount { get; }

    /// <summary>Cộng 2 khoản tiền.</summary>
    public static Money operator +(Money left, Money right) => new(left.Amount + right.Amount);

    /// <summary>Trừ; kết quả âm → ArgumentException (vd giảm giá lớn hơn tổng — Discount đã tự chặn).</summary>
    public static Money operator -(Money left, Money right) => new(left.Amount - right.Amount);

    /// <summary>Nhân với số lượng (đơn giá × số ly).</summary>
    public static Money operator *(Money price, int quantity) => new(price.Amount * quantity);

    /// <summary>So sánh lớn hơn.</summary>
    public static bool operator >(Money left, Money right) => left.Amount > right.Amount;

    /// <summary>So sánh nhỏ hơn.</summary>
    public static bool operator <(Money left, Money right) => left.Amount < right.Amount;

    /// <summary>So sánh lớn hơn hoặc bằng.</summary>
    public static bool operator >=(Money left, Money right) => left.Amount >= right.Amount;

    /// <summary>So sánh nhỏ hơn hoặc bằng.</summary>
    public static bool operator <=(Money left, Money right) => left.Amount <= right.Amount;

    /// <summary>Tổng 1 dãy khoản tiền (dãy rỗng = 0 đ).</summary>
    public static Money Sum(IEnumerable<Money> amounts) => amounts.Aggregate(Zero, (total, next) => total + next);

    /// <inheritdoc />
    public int CompareTo(Money other) => this.Amount.CompareTo(other.Amount);

    /// <summary>"83.000 đ" — dùng khi ghép chuỗi thông báo.</summary>
    public override string ToString() => $"{this.Amount:N0} đ";

    // Có 2 overload dưới → Razor ở Web viết @item.UnitPrice.ToString("N0") vẫn biên dịch như khi UnitPrice còn là decimal.
    /// <summary>Định dạng phần số (vd "N0") theo văn hóa hiện tại.</summary>
    public string ToString(string? format) => this.Amount.ToString(format, CultureInfo.CurrentCulture);

    /// <inheritdoc />
    public string ToString(string? format, IFormatProvider? formatProvider) => this.Amount.ToString(format, formatProvider);
}
