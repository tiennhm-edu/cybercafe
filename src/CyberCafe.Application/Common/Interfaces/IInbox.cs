// ============================================================================
// IInbox.cs — port "message này đã xử lý chưa?" cho IDEMPOTENT CONSUMER (Buổi 55).
// RabbitMQ (và mọi broker) chỉ hứa "at-least-once": 1 message có thể tới 2 lần (consumer xử lý xong nhưng
// sập trước khi ack, outbox gửi lại sau khi mất kết nối...). Xử lý 2 lần "PaymentCompleted" = cộng điểm 2 lần.
// Cách chữa: ghi lại EventId đã xử lý (bảng ProcessedMessages) TRONG CÙNG transaction với thay đổi nghiệp vụ.
// Lần sau gặp lại EventId đó → bỏ qua. Khóa chính (MessageId, Consumer) là chốt chặn cuối cùng khi 2 bản
// trùng chạy SONG SONG (cả 2 cùng thấy "chưa xử lý") — bản thứ 2 vi phạm khóa → rollback → retry → thấy "đã xử lý".
// ============================================================================
namespace CyberCafe.Application.Common.Interfaces;

// 👉 Bước 5 (b55.md)
/// <summary>Nhật ký message đã xử lý của service này.</summary>
public interface IInbox
{
    /// <summary><paramref name="consumer"/> đã xử lý message <paramref name="messageId"/> chưa.</summary>
    Task<bool> HasProcessedAsync(Guid messageId, string consumer, CancellationToken ct = default);

    /// <summary>Đánh dấu đã xử lý — được lưu cùng lần SaveChanges với thay đổi nghiệp vụ.</summary>
    void MarkProcessed(Guid messageId, string consumer);
}
