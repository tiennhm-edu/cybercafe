// ============================================================================
// OrderStatusFlow.cs — luồng trạng thái đơn (Buổi 32–41 · state machine đơn giản).
// Sơ đồ:
//   Pending ──► Preparing ──► Ready ──► Completed
//      │            │
//      └────────────┴──► Cancelled      (đã Ready/Completed thì KHÔNG hủy được nữa)
// Vì sao để trong Domain chứ không để trong controller?
//   - Barista bấm nút (Web), API kiểm tra (Api), test xUnit — cả 3 dùng CHUNG 1 bảng luật.
//   - Web dùng NextSteps(...) để chỉ hiện các nút hợp lệ → người dùng khó bấm sai.
// ============================================================================
namespace CyberCafe.Domain.Orders;

/// <summary>
/// Bảng luật chuyển trạng thái đơn: trạng thái hiện tại → các trạng thái được phép đi tiếp.
/// </summary>
public static class OrderStatusFlow
{
    // Dictionary "từ → được phép tới": thêm luật mới chỉ sửa 1 chỗ này
    private static readonly Dictionary<OrderStatus, OrderStatus[]> Allowed = new()
    {
        [OrderStatus.Pending] = [OrderStatus.Preparing, OrderStatus.Cancelled],
        [OrderStatus.Preparing] = [OrderStatus.Ready, OrderStatus.Cancelled],
        [OrderStatus.Ready] = [OrderStatus.Completed],
        [OrderStatus.Completed] = [],
        [OrderStatus.Cancelled] = [],
    };

    /// <summary>Có được chuyển từ <paramref name="from"/> sang <paramref name="to"/> không.</summary>
    public static bool CanChange(OrderStatus from, OrderStatus to) =>
        Allowed.TryGetValue(from, out OrderStatus[]? next) && next.Contains(to);

    /// <summary>Các trạng thái kế tiếp hợp lệ (dùng để vẽ nút trên màn hình barista).</summary>
    public static IReadOnlyList<OrderStatus> NextSteps(OrderStatus from) =>
        Allowed.TryGetValue(from, out OrderStatus[]? next) ? next : [];

    /// <summary>Đơn còn "đang chạy" (barista cần thấy) hay đã kết thúc.</summary>
    public static bool IsActive(OrderStatus status) =>
        status is OrderStatus.Pending or OrderStatus.Preparing or OrderStatus.Ready;

    /// <summary>Tên tiếng Việt để hiển thị (dùng chung cho thông báo lỗi và giao diện).</summary>
    public static string Describe(OrderStatus status) => status switch
    {
        OrderStatus.Pending => "Chờ pha chế",
        OrderStatus.Preparing => "Đang pha chế",
        OrderStatus.Ready => "Sẵn sàng",
        OrderStatus.Completed => "Hoàn tất",
        _ => "Đã hủy"
    };
}
