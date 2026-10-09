// ============================================================================
// OutboxMessage.cs — 1 dòng của bảng OutboxMessages (Buổi 55 · Outbox pattern).
// Không phải domain (Order không biết outbox tồn tại) → class kỹ thuật, nằm ở Infrastructure.
// Vòng đời 1 dòng:
//   (1) Command handler Enqueue → INSERT cùng transaction với đơn hàng (ProcessedAtUtc = NULL).
//   (2) OutboxDispatcher đọc các dòng NULL → publish lên RabbitMQ → ghi ProcessedAtUtc.
//   (3) Publish lỗi → Attempts++, LastError; lần quét sau thử lại. Quá MaxAttempts → dừng, chờ người kiểm tra.
// Sập giữa (2) "publish xong" và "ghi ProcessedAtUtc" → lần sau gửi LẠI → bên nhận phải idempotent (EventId).
// Đó là lý do Outbox cho "at-least-once", KHÔNG phải "exactly-once".
// ============================================================================
using System.Text.Json;
using CyberCafe.IntegrationEvents;

namespace CyberCafe.Infrastructure.Messaging;

// 👉 Bước 2 (b55.md)
/// <summary>Message chờ gửi lên broker.</summary>
public class OutboxMessage
{
    // JSON chuẩn web (camelCase) — message đọc lại bằng đúng options này
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>= EventId của integration event (khóa chính → không bao giờ có 2 dòng cho cùng 1 event).</summary>
    public Guid Id { get; private set; }

    /// <summary>Tên kiểu đầy đủ, vd "CyberCafe.IntegrationEvents.OrderPlacedIntegrationEvent" — để đọc lại đúng kiểu.</summary>
    public string Type { get; private set; } = string.Empty;

    /// <summary>Nội dung event dạng JSON.</summary>
    public string Payload { get; private set; } = string.Empty;

    /// <summary>Thời điểm event xảy ra (UTC) — dispatcher gửi theo thứ tự này.</summary>
    public DateTime OccurredAtUtc { get; private set; }

    /// <summary>
    /// W3C "traceparent" của request đã tạo event (Activity.Current.Id). Dispatcher chạy ở luồng nền — không có trace;
    /// đọc lại giá trị này để span "outbox publish" là CON của request POST /api/orders → dashboard thấy 1 trace liền mạch.
    /// </summary>
    public string? TraceParent { get; private set; }

    /// <summary>Đã gửi lúc nào (NULL = chưa gửi).</summary>
    public DateTime? ProcessedAtUtc { get; set; }

    /// <summary>Số lần gửi thất bại.</summary>
    public int Attempts { get; set; }

    /// <summary>Lỗi gần nhất (cắt ngắn) — để tra cứu khi message "kẹt".</summary>
    public string? LastError { get; set; }

    /// <summary>Đóng gói 1 integration event thành dòng outbox.</summary>
    public static OutboxMessage From(IIntegrationEvent integrationEvent, string? traceParent) => new()
    {
        Id = integrationEvent.EventId,
        // ⚠️ Lỗi hay gặp: đổi tên/namespace class event khi outbox còn dòng chưa gửi → không đọc lại được kiểu.
        //    Integration event là hợp đồng: đổi tên = tạo event mới, giữ class cũ tới khi outbox sạch.
        Type = integrationEvent.GetType().FullName!,
        // Serialize theo kiểu THẬT (GetType()), không theo interface — nếu không JSON chỉ có EventId + OccurredAtUtc
        Payload = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType(), Json),
        OccurredAtUtc = integrationEvent.OccurredAtUtc,
        TraceParent = traceParent,
    };

    /// <summary>Đọc lại event từ JSON (kiểu tìm trong assembly CyberCafe.IntegrationEvents).</summary>
    /// <exception cref="InvalidOperationException">Không tìm thấy kiểu (đã đổi tên?) hoặc JSON hỏng.</exception>
    public IIntegrationEvent ToIntegrationEvent()
    {
        Type type = typeof(IIntegrationEvent).Assembly.GetType(this.Type)
            ?? throw new InvalidOperationException($"Không tìm thấy kiểu integration event '{this.Type}'");
        return JsonSerializer.Deserialize(this.Payload, type, Json) as IIntegrationEvent
            ?? throw new InvalidOperationException($"Payload của outbox {this.Id} không hợp lệ");
    }
}

/// <summary>1 dòng của bảng ProcessedMessages (idempotent consumer — Buổi 55).</summary>
public class ProcessedMessage
{
    /// <summary>EventId đã xử lý.</summary>
    public Guid MessageId { get; set; }

    /// <summary>Ai đã xử lý (1 event có thể được nhiều consumer khác nhau xử lý độc lập).</summary>
    public string Consumer { get; set; } = string.Empty;

    /// <summary>Thời điểm xử lý (UTC).</summary>
    public DateTime ProcessedAtUtc { get; set; }
}
