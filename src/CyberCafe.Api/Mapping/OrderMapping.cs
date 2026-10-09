// ============================================================================
// OrderMapping.cs — Domain Order → OrderDto (Buổi 32–41).
// Khác ProductMapping (projection trong SQL): đơn hàng cần LOGIC DOMAIN để ra số liệu
// (Discount.GetDiscountAmount đa hình, Payment.Display()...) → không dịch sang SQL được.
// Cách làm: Include đủ dữ liệu → EF dựng object domain → map trong bộ nhớ.
// ============================================================================
using CyberCafe.Contracts.Orders;
using CyberCafe.Domain.Orders;

namespace CyberCafe.Api.Mapping;

/// <summary>Chuyển Order (đã Include Items.Product, Discount, Payment) thành OrderDto.</summary>
public static class OrderMapping
{
    /// <summary>
    /// Map đơn hàng. Yêu cầu đã Include đủ navigation —
    /// ⚠️ Lỗi hay gặp: quên .ThenInclude(i => i.Product) → item.Product là null → NullReferenceException.
    /// </summary>
    public static OrderDto ToDto(this Order order) => new(
        order.Id,
        order.Code,
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
            i.UnitPrice,
            i.TotalPrice)).ToList(),
        order.TotalAmount,
        order.Discount?.Display(),  // đa hình: Voucher hiện mã, Member hiện %
        order.DiscountAmount,
        order.FinalAmount,
        order.Payment?.Method,
        order.Payment?.Display());  // đa hình: Cash/Card/Momo tự mô tả
}
