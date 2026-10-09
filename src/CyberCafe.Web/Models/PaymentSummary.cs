// ============================================================================
// PaymentSummary.cs — hình dạng JSON của 1 giao dịch từ Payment service (Buổi 54).
// Trùng TÊN field với PaymentView bên CyberCafe.Payment.Api (JSON camelCase) nhưng là class RIÊNG của Web:
// Web không tham chiếu project của Payment service (ranh giới service — architecture test kiểm tra phía service).
// ============================================================================
namespace CyberCafe.Web.Models;

/// <summary>1 giao dịch thanh toán (màn hình admin /admin/payments).</summary>
/// <param name="OrderId">Id đơn.</param>
/// <param name="OrderCode">Mã đơn (CC-0007).</param>
/// <param name="Amount">Số tiền.</param>
/// <param name="Method">"Cash" / "Card" / "Momo".</param>
/// <param name="CardLast4">4 số cuối thẻ.</param>
/// <param name="Status">"Succeeded" / "Failed".</param>
/// <param name="FailureReason">Lý do từ chối.</param>
/// <param name="TransactionId">Mã giao dịch.</param>
/// <param name="ProcessedAtUtc">Thời điểm xử lý (UTC).</param>
public record PaymentSummary(
    int OrderId,
    string OrderCode,
    decimal Amount,
    string Method,
    string? CardLast4,
    string Status,
    string? FailureReason,
    string TransactionId,
    DateTime ProcessedAtUtc);
