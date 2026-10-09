// ============================================================================
// EfOutboxAndInbox.cs — adapter EF Core cho 2 port IIntegrationEventOutbox / IInbox (Buổi 55).
// Cả 2 chỉ "Add" vào CHÍNH DbContext của request/message đang chạy → được lưu ở lần SaveChanges kế tiếp,
// cùng transaction với aggregate. Đó là toàn bộ bí mật của Outbox: không có gì "gửi" ở đây cả.
// ⚠️ Lỗi hay gặp: tự new DbContext riêng (hoặc AddDbContext lần 2) cho outbox → khác transaction → mất ý nghĩa.
// ============================================================================
using System.Diagnostics;
using CyberCafe.Application.Common.Interfaces;
using CyberCafe.Infrastructure.Persistence;
using CyberCafe.IntegrationEvents;
using Microsoft.EntityFrameworkCore;

namespace CyberCafe.Infrastructure.Messaging;

// 👉 Bước 2 (b55.md)
/// <summary>Outbox trên bảng OutboxMessages (cùng DbContext với đơn hàng).</summary>
public sealed class EfIntegrationEventOutbox(CyberCafeDbContext db) : IIntegrationEventOutbox
{
    /// <inheritdoc />
    public void Enqueue(IIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        // Activity.Current = span của request HTTP hiện tại (ASP.NET Core instrumentation tạo) → lưu lại để nối trace
        db.OutboxMessages.Add(OutboxMessage.From(integrationEvent, Activity.Current?.Id));
    }
}

// 👉 Bước 5 (b55.md)
/// <summary>Inbox trên bảng ProcessedMessages.</summary>
public sealed class EfInbox(CyberCafeDbContext db, TimeProvider clock) : IInbox
{
    /// <inheritdoc />
    public Task<bool> HasProcessedAsync(Guid messageId, string consumer, CancellationToken ct = default) =>
        db.ProcessedMessages.AnyAsync(m => m.MessageId == messageId && m.Consumer == consumer, ct);

    /// <inheritdoc />
    public void MarkProcessed(Guid messageId, string consumer) =>
        db.ProcessedMessages.Add(new ProcessedMessage
        {
            MessageId = messageId,
            Consumer = consumer,
            ProcessedAtUtc = clock.GetUtcNow().UtcDateTime,
        });
}
