// ============================================================================
// OrderCode.cs — VALUE OBJECT mã đơn hiển thị "CC-0007" (Buổi 50 · value object).
// Trước b53: Order.FormatCode(int) trả string; ai muốn ĐỌC ngược mã khách đọc qua điện thoại ("CC-0007" → 7)
// lại tự viết Substring/int.Parse ở chỗ khác. Gom cả 2 chiều (tạo + phân tích) vào 1 kiểu.
// Không lưu DB: mã tính từ Id (IDENTITY) nên luôn khớp, không cần cột riêng.
// ============================================================================
using System.Globalization;

namespace CyberCafe.Domain.Orders;

// 👉 Bước 3 (b50.md)
/// <summary>Mã đơn "CC-" + Id 4 chữ số (Id lớn hơn thì dài hơn: CC-12345).</summary>
public readonly record struct OrderCode
{
    private const string Prefix = "CC-";

    private OrderCode(int orderId) => this.OrderId = orderId;

    /// <summary>Id đơn tương ứng.</summary>
    public int OrderId { get; }

    /// <summary>Chuỗi hiển thị, vd "CC-0007".</summary>
    public string Value => $"{Prefix}{this.OrderId:0000}";

    /// <summary>Mã của đơn có Id cho trước (Id = 0: đơn chưa lưu → "CC-0000").</summary>
    public static OrderCode From(int orderId) =>
        orderId < 0 ? throw new ArgumentException("Mã đơn không hợp lệ") : new OrderCode(orderId);

    /// <summary>Đọc mã khách đưa ("cc-0007", " CC-7 ") → OrderCode; sai định dạng trả false.</summary>
    public static bool TryParse(string? text, out OrderCode code)
    {
        code = default;
        string value = (text ?? string.Empty).Trim();
        if (!value.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)
            || !int.TryParse(value[Prefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out int id))
        {
            return false;
        }

        code = new OrderCode(id);
        return true;
    }

    /// <summary>Trả về <see cref="Value"/>.</summary>
    public override string ToString() => this.Value;
}
