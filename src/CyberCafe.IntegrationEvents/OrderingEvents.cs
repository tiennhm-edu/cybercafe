// ============================================================================
// OrderingEvents.cs — message giữa Order service (CyberCafe.Api) và Payment service (Buổi 55 · luồng sự kiện).
//
//   Api (Order)                         RabbitMQ                          Payment.Api
//   POST /api/orders ─► Outbox ──► OrderPlacedIntegrationEvent ──► OrderPlacedConsumer (luật thanh toán)
//                                                                         │
//   PaymentCompletedConsumer ◄── PaymentCompletedIntegrationEvent ◄───────┤ (thẻ hợp lệ, trong hạn mức)
//   PaymentFailedConsumer    ◄── PaymentFailedIntegrationEvent    ◄───────┘ (vượt hạn mức / thẻ bị từ chối)
//
// Đặt tên thì QUÁ KHỨ (đã xảy ra) như domain event. Hậu tố "IntegrationEvent" để không nhầm với domain event
// cùng tên OrderPlaced (Buổi 50) — hai thứ khác nhau: xem bảng trong IIntegrationEvent.cs.
// ⚠️ Lỗi hay gặp: gửi SỐ THẺ đầy đủ qua broker → dữ liệu thẻ nằm trong queue, log, file dump. Chỉ gửi 4 số cuối.
// ============================================================================
namespace CyberCafe.IntegrationEvents;

// 👉 Bước 1 (b55.md)
/// <summary>Order service: có đơn mới cần thanh toán (đơn đang Pending, chưa trả tiền).</summary>
/// <param name="EventId">Id sự kiện (khóa chống trùng).</param>
/// <param name="OccurredAtUtc">Thời điểm (UTC).</param>
/// <param name="OrderId">Id đơn bên Order service — Payment chỉ giữ Id, không biết gì thêm về Order.</param>
/// <param name="OrderCode">Mã hiển thị, vd CC-0007 (cho log/màn hình admin dễ đọc).</param>
/// <param name="Amount">Số tiền phải trả (server đã tính, sau giảm giá).</param>
/// <param name="PaymentMethod">"Cash" / "Card" / "Momo" — CHUỖI, không dùng enum của Domain.</param>
/// <param name="CardLast4">4 số cuối thẻ (chỉ khi Card), null nếu không phải thẻ.</param>
public sealed record OrderPlacedIntegrationEvent(
    Guid EventId,
    DateTime OccurredAtUtc,
    int OrderId,
    string OrderCode,
    decimal Amount,
    string PaymentMethod,
    string? CardLast4) : IIntegrationEvent;

/// <summary>Payment service: đã thu tiền thành công cho đơn <paramref name="OrderId"/>.</summary>
/// <param name="EventId">Id sự kiện (gửi lại vẫn giữ nguyên).</param>
/// <param name="OccurredAtUtc">Thời điểm (UTC).</param>
/// <param name="OrderId">Đơn đã thanh toán.</param>
/// <param name="Amount">Số tiền đã thu.</param>
/// <param name="PaymentMethod">Hình thức (giống trong OrderPlaced).</param>
/// <param name="CardLast4">4 số cuối thẻ (nếu Card).</param>
/// <param name="TransactionId">Mã giao dịch do Payment service sinh (để đối soát).</param>
public sealed record PaymentCompletedIntegrationEvent(
    Guid EventId,
    DateTime OccurredAtUtc,
    int OrderId,
    decimal Amount,
    string PaymentMethod,
    string? CardLast4,
    string TransactionId) : IIntegrationEvent;

/// <summary>Payment service: từ chối thanh toán → Order service hủy đơn.</summary>
/// <param name="EventId">Id sự kiện.</param>
/// <param name="OccurredAtUtc">Thời điểm (UTC).</param>
/// <param name="OrderId">Đơn bị từ chối.</param>
/// <param name="Reason">Lý do (tiếng Việt, hiển thị được cho khách).</param>
public sealed record PaymentFailedIntegrationEvent(
    Guid EventId,
    DateTime OccurredAtUtc,
    int OrderId,
    string Reason) : IIntegrationEvent;
