// ============================================================================
// OrderStatus.cs — enum trạng thái đơn (Buổi 24–31 · enum).
// enum = tập giá trị cố định có tên → an toàn hơn dùng string "pending"/"ready" (gõ sai là lỗi biên dịch).
// ============================================================================
namespace CyberCafe.Domain.Orders;

/// <summary>
/// Trạng thái đơn. Buổi 33–34 quầy barista sẽ chuyển Pending → Preparing → Ready → Completed.
/// </summary>
public enum OrderStatus
{
    /// <summary>Vừa đặt, chờ pha chế.</summary>
    Pending,

    /// <summary>Barista đang pha chế.</summary>
    Preparing,

    /// <summary>Đã xong, chờ khách nhận.</summary>
    Ready,

    /// <summary>Khách đã nhận.</summary>
    Completed,

    /// <summary>Đơn bị hủy.</summary>
    Cancelled
}
