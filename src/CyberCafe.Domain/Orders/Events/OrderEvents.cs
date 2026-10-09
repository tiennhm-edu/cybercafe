// ============================================================================
// OrderEvents.cs — domain event của aggregate Order (Buổi 50 · domain event).
//   OrderPaid          — đã thu tiền (Order.Pay).
//   OrderPlaced        — đơn vào hàng chờ của quầy. Luật của quán: TRẢ TIỀN XONG mới là "đặt" →
//                        Pay() phát cả OrderPaid lẫn OrderPlaced; đơn chưa trả tiền không bao giờ hiện ở quầy.
//   OrderStatusChanged — Pending → Preparing → Ready → Completed / Cancelled.
// Vì sao event giữ THAM CHIẾU tới Order thay vì chỉ OrderId?
//   Id do SQL Server sinh (IDENTITY) → lúc Raise() Id còn = 0. Event được phát SAU SaveChanges, khi đó
//   order.Id đã có giá trị → handler đọc order.Id / order.ToDto() là đúng.
//   ⚠️ Đổi lại: handler chỉ được ĐỌC order, không sửa (sửa ở đây không ai lưu nữa).
// ============================================================================
using CyberCafe.Domain.Common;

namespace CyberCafe.Domain.Orders.Events;

// 👉 Bước 5 (b50.md)
/// <summary>Đơn đã vào hàng chờ pha chế (phát cùng lúc với <see cref="OrderPaid"/>).</summary>
/// <param name="Order">Đơn vừa đặt.</param>
/// <param name="OccurredAt">Thời điểm.</param>
public sealed record OrderPlaced(Order Order, DateTime OccurredAt) : IDomainEvent;

/// <summary>Đơn đã thanh toán.</summary>
/// <param name="Order">Đơn đã thanh toán.</param>
/// <param name="Amount">Số tiền đã thu.</param>
/// <param name="OccurredAt">Thời điểm.</param>
public sealed record OrderPaid(Order Order, Money Amount, DateTime OccurredAt) : IDomainEvent;

/// <summary>Đơn đổi trạng thái.</summary>
/// <param name="Order">Đơn vừa đổi.</param>
/// <param name="From">Trạng thái cũ.</param>
/// <param name="To">Trạng thái mới.</param>
/// <param name="OccurredAt">Thời điểm.</param>
public sealed record OrderStatusChanged(Order Order, OrderStatus From, OrderStatus To, DateTime OccurredAt) : IDomainEvent;
