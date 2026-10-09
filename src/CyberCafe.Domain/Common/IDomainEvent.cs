// ============================================================================
// IDomainEvent.cs — "điều gì đó QUAN TRỌNG đã xảy ra trong nghiệp vụ" (Buổi 50 · domain event).
// Đặt tên ở THÌ QUÁ KHỨ: OrderPlaced, OrderPaid, OrderStatusChanged — sự kiện là sự thật đã xảy ra, không từ chối được.
// Khác "command" (PlaceOrder — yêu cầu, có thể bị từ chối) sẽ gặp ở buổi 51.
// ============================================================================
namespace CyberCafe.Domain.Common;

/// <summary>Đánh dấu 1 domain event (record bất biến, raise từ bên trong aggregate).</summary>
public interface IDomainEvent
{
    /// <summary>Thời điểm xảy ra (giờ máy chủ, giống Order.CreatedAt).</summary>
    DateTime OccurredAt { get; }
}
