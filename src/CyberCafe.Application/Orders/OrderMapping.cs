// ============================================================================
// OrderMapping.cs — Domain Order → OrderDto (Buổi 32–41 · 48 · 50).
// Khác ProductMapping (projection trong SQL): đơn hàng cần LOGIC DOMAIN để ra số liệu
// (Discount.GetDiscountAmount đa hình, Payment.Display()...) → không dịch sang SQL được.
// Cách làm: Include đủ dữ liệu → EF dựng object domain → map trong bộ nhớ.
// Buổi 48: chuyển từ Api/Mapping sang Application.
// Buổi 50: tiền là Money, mã là OrderCode (value object) → DTO (hợp đồng JSON với Web) vẫn là decimal/string:
//   value object là chuyện BÊN TRONG Domain, không làm đổi hợp đồng HTTP.
// Buổi 51–53: dùng cho phía GHI (kết quả command, domain event). Phía ĐỌC có projection riêng (OrderReadStore).
// ============================================================================
using CyberCafe.Contracts.Orders;
using CyberCafe.Domain.Orders;

namespace CyberCafe.Application.Orders;

/// <summary>Chuyển Order (đã Include Items.Product, Discount, Payment) thành OrderDto.</summary>
public static class OrderMapping
{
    /// <summary>
    /// Map đơn hàng. Yêu cầu đã Include đủ navigation —
    /// ⚠️ Lỗi hay gặp: quên .ThenInclude(i => i.Product) → item.Product là null → NullReferenceException.
    /// </summary>
    public static OrderDto ToDto(this Order order) => new(
        order.Id,
        order.Code.Value,           // OrderCode → "CC-0007"
        order.Status,
        order.CreatedAt,
        order.Customer.FullName,
        order.Customer.PhoneNumber,
        order.Customer.LoyaltyPoints,
        order.Note,
        order.Items.Select(i => new OrderItemDto(
            i.Product.Id,
            i.Product.Name,
            i.Product.Emoji,
            i.Product.HasSize,
            i.Size,
            i.Quantity,
            i.UnitPrice.Amount,     // Money → decimal
            i.TotalPrice.Amount)).ToList(),
        order.TotalAmount.Amount,
        order.Discount?.Display(),  // đa hình: Voucher hiện mã, Member hiện %
        order.DiscountAmount.Amount,
        order.FinalAmount.Amount,
        order.Payment?.Method,
        order.Payment?.Display());  // đa hình: Cash/Card/Momo tự mô tả
}
