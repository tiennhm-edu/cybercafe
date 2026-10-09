// ============================================================================
// PaymentDbContext.cs — database RIÊNG của Payment service (Buổi 54 · "database per service").
// Payment KHÔNG đọc bảng Orders của Order service (và ngược lại). Cần gì của bên kia thì:
//   - nhận qua message (OrderPlacedIntegrationEvent mang sẵn OrderId, số tiền, hình thức), hoặc
//   - gọi API của bên kia.
// ⚠️ Lỗi hay gặp: 2 service dùng chung 1 database "cho nhanh" → đổi 1 cột bên này làm sập bên kia; không deploy
//    riêng được nữa = "distributed monolith" (có đủ cái khó của microservice mà không được cái lợi nào).
// Vì sao không dùng lại aggregate/DDD đầy đủ như Order? Service nhỏ, luật đơn giản → entity + 1 class luật là đủ.
// Mỗi service tự chọn kiến trúc bên trong phù hợp độ phức tạp của nó.
// ============================================================================
using Microsoft.EntityFrameworkCore;

namespace CyberCafe.Payment.Api.Data;

// 👉 Bước 2 (b54.md)
/// <summary>EF Core cho database CyberCafePayments.</summary>
public class PaymentDbContext(DbContextOptions<PaymentDbContext> options) : DbContext(options)
{
    /// <summary>Giao dịch thanh toán (mỗi đơn tối đa 1 giao dịch).</summary>
    public DbSet<PaymentTransaction> Transactions => this.Set<PaymentTransaction>();

    /// <summary>Message đã xử lý (idempotent consumer).</summary>
    public DbSet<ProcessedMessage> ProcessedMessages => this.Set<ProcessedMessage>();

    /// <summary>Fluent API — ít bảng nên cấu hình ngay tại đây (Order service tách ra Configurations/).</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PaymentTransaction>(tx =>
        {
            tx.ToTable("Payments");
            // UNIQUE OrderId: chốt chặn ở DB — dù message trùng lọt qua mọi kiểm tra trong code, 1 đơn vẫn không bị thu 2 lần
            tx.HasIndex(t => t.OrderId).IsUnique();
            tx.Property(t => t.OrderCode).HasMaxLength(20);
            tx.Property(t => t.Amount).HasPrecision(18, 2);
            tx.Property(t => t.Method).HasMaxLength(10);
            tx.Property(t => t.CardLast4).HasMaxLength(4);
            tx.Property(t => t.Status).HasConversion<string>().HasMaxLength(10);
            tx.Property(t => t.FailureReason).HasMaxLength(200);
            tx.Property(t => t.TransactionId).HasMaxLength(40);
            tx.HasIndex(t => t.ProcessedAtUtc);
        });

        modelBuilder.Entity<ProcessedMessage>(m =>
        {
            m.ToTable("ProcessedMessages");
            m.HasKey(x => new { x.MessageId, x.Consumer });
            m.Property(x => x.Consumer).HasMaxLength(100);
        });
    }
}

/// <summary>Kết quả 1 giao dịch.</summary>
public enum PaymentStatus
{
    /// <summary>Đã thu tiền.</summary>
    Succeeded,

    /// <summary>Bị từ chối.</summary>
    Failed,
}

/// <summary>1 giao dịch thanh toán cho 1 đơn của Order service.</summary>
public class PaymentTransaction
{
    /// <summary>Khóa chính (IDENTITY).</summary>
    public int Id { get; set; }

    /// <summary>Id đơn bên Order service — chỉ là 1 con số, KHÔNG có khóa ngoại sang database khác.</summary>
    public int OrderId { get; set; }

    /// <summary>Mã đơn hiển thị (CC-0007).</summary>
    public string OrderCode { get; set; } = string.Empty;

    /// <summary>Số tiền.</summary>
    public decimal Amount { get; set; }

    /// <summary>"Cash" / "Card" / "Momo".</summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>4 số cuối thẻ (nếu Card) — Payment cũng không bao giờ nhận số thẻ đầy đủ trong demo này.</summary>
    public string? CardLast4 { get; set; }

    /// <summary>Thành công / thất bại.</summary>
    public PaymentStatus Status { get; set; }

    /// <summary>Lý do từ chối (nếu thất bại).</summary>
    public string? FailureReason { get; set; }

    /// <summary>Mã giao dịch sinh ra (đối soát), vd "PAY-20261009-1A2B3C4D".</summary>
    public string TransactionId { get; set; } = string.Empty;

    /// <summary>EventId của OrderPlacedIntegrationEvent đã tạo giao dịch này.</summary>
    public Guid RequestEventId { get; set; }

    /// <summary>
    /// EventId của message KẾT QUẢ đã gửi. Lưu lại để khi phải gửi lại (message trùng) thì gửi ĐÚNG EventId cũ
    /// → Order service nhận ra là trùng và bỏ qua.
    /// </summary>
    public Guid ResultEventId { get; set; }

    /// <summary>Thời điểm xử lý (UTC).</summary>
    public DateTime ProcessedAtUtc { get; set; }
}

/// <summary>Message đã xử lý (giống bảng cùng tên bên Order service — mỗi service 1 bản riêng).</summary>
public class ProcessedMessage
{
    /// <summary>EventId.</summary>
    public Guid MessageId { get; set; }

    /// <summary>Tên consumer.</summary>
    public string Consumer { get; set; } = string.Empty;

    /// <summary>Thời điểm xử lý (UTC).</summary>
    public DateTime ProcessedAtUtc { get; set; }
}
