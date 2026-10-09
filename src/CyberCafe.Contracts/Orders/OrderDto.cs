// ============================================================================
// OrderDto.cs — dữ liệu đơn hàng trả về từ API và đẩy qua SignalR (Buổi 32–41).
// Các con số (tổng, giảm, phải trả) đã được SERVER tính bằng domain (đa hình Discount/Payment)
// → trang Web và màn hình barista chỉ hiển thị, không tự tính lại.
// ============================================================================
using System.ComponentModel.DataAnnotations;
using CyberCafe.Domain.Orders;
using CyberCafe.Domain.Payments;
using CyberCafe.Domain.Products;

namespace CyberCafe.Contracts.Orders;

/// <summary>1 dòng của đơn đã lưu (đơn giá là giá LÚC ĐẶT).</summary>
/// <param name="ProductId">Mã món.</param>
/// <param name="ProductName">Tên món.</param>
/// <param name="Emoji">Emoji của món.</param>
/// <param name="HasSize">Món có size không (để ẩn chữ "size S" với bánh).</param>
/// <param name="Size">Size đã chọn.</param>
/// <param name="Quantity">Số lượng.</param>
/// <param name="UnitPrice">Đơn giá theo size lúc đặt.</param>
/// <param name="LineTotal">Thành tiền dòng.</param>
public record OrderItemDto(
    int ProductId,
    string ProductName,
    string Emoji,
    bool HasSize,
    DrinkSize Size,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

/// <summary>Đơn hàng đầy đủ (GET /api/orders/{id}, sự kiện SignalR OrderPlaced/OrderStatusChanged).</summary>
/// <param name="Id">Khóa chính (số).</param>
/// <param name="Code">Mã hiển thị, vd CC-0007.</param>
/// <param name="Status">Trạng thái hiện tại.</param>
/// <param name="CreatedAt">Thời điểm đặt.</param>
/// <param name="CustomerName">Họ tên khách.</param>
/// <param name="PhoneNumber">SĐT khách.</param>
/// <param name="LoyaltyPoints">Điểm tích lũy của khách sau đơn này.</param>
/// <param name="Note">Ghi chú (có thể null).</param>
/// <param name="Items">Các dòng món.</param>
/// <param name="TotalAmount">Tạm tính trước giảm giá.</param>
/// <param name="DiscountText">Mô tả giảm giá (Discount.Display()), null nếu không có.</param>
/// <param name="DiscountAmount">Số tiền được giảm.</param>
/// <param name="FinalAmount">Số tiền phải trả.</param>
/// <param name="PaymentMethod">Hình thức thanh toán.</param>
/// <param name="PaymentText">Mô tả thanh toán (Payment.Display()).</param>
public record OrderDto(
    int Id,
    string Code,
    OrderStatus Status,
    DateTime CreatedAt,
    string CustomerName,
    string PhoneNumber,
    int LoyaltyPoints,
    string? Note,
    IReadOnlyList<OrderItemDto> Items,
    decimal TotalAmount,
    string? DiscountText,
    decimal DiscountAmount,
    decimal FinalAmount,
    PaymentMethod? PaymentMethod,
    string? PaymentText);

/// <summary>Body của PUT /api/orders/{id}/status.</summary>
public class ChangeOrderStatusRequest
{
    /// <summary>
    /// Trạng thái mới. Kiểu nullable + [Required]: thiếu field → 400 rõ ràng,
    /// thay vì âm thầm nhận giá trị mặc định Pending.
    /// </summary>
    [Required(ErrorMessage = "Vui lòng chọn trạng thái mới")]
    public OrderStatus? Status { get; set; }
}
