// ============================================================================
// MessagingConfiguration.cs — bảng OutboxMessages + ProcessedMessages (Buổi 55 · Outbox + idempotent consumer).
// Sinh bởi migration AddOutboxAndInbox. 2 bảng nằm CHUNG database với Orders — bắt buộc: transaction của
// SQL Server chỉ bao được các bảng trong cùng database. Outbox ở database khác = lại "dual write".
// ============================================================================
using CyberCafe.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberCafe.Infrastructure.Persistence.Configurations;

// 👉 Bước 2 (b55.md)
/// <summary>Cấu hình bảng OutboxMessages.</summary>
public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        // Khóa = EventId (Guid do code sinh) → ValueGeneratedNever: EF không chờ DB sinh khóa
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Type).HasMaxLength(200).IsRequired();
        builder.Property(m => m.Payload).IsRequired();            // nvarchar(max): JSON
        builder.Property(m => m.TraceParent).HasMaxLength(100);
        builder.Property(m => m.LastError).HasMaxLength(500);

        // Dispatcher hỏi "dòng nào chưa gửi, cũ nhất trước" mỗi giây → index đúng 2 cột đó.
        // (SQL Server có filtered index "WHERE ProcessedAtUtc IS NULL" còn gọn hơn — bài tập.)
        builder.HasIndex(m => new { m.ProcessedAtUtc, m.OccurredAtUtc });
    }
}

// 👉 Bước 5 (b55.md)
/// <summary>Cấu hình bảng ProcessedMessages.</summary>
public class ProcessedMessageConfiguration : IEntityTypeConfiguration<ProcessedMessage>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ProcessedMessage> builder)
    {
        builder.ToTable("ProcessedMessages");
        // Khóa chính GHÉP (MessageId, Consumer): database tự chặn "xử lý 2 lần" kể cả khi 2 bản trùng chạy song song
        builder.HasKey(m => new { m.MessageId, m.Consumer });
        builder.Property(m => m.Consumer).HasMaxLength(100);
    }
}
