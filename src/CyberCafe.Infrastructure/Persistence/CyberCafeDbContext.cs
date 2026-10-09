// ============================================================================
// CyberCafeDbContext.cs — "cửa ngõ" EF Core tới database CyberCafeDb (Buổi 36–41 · 48 · 50 · 52 · 55 · Code First).
// Code First: viết class C# trước (chính là class domain Product/Order...) → EF sinh bảng
// bằng migration. Không có class "entity" riêng: EF lưu thẳng domain nhờ Fluent API
// (thư mục Configurations/) — domain không phải gắn [Key], [Table]... nên vẫn là C# thuần.
// Buổi 48: chuyển từ Api/Data sang Infrastructure/Persistence; implement IUnitOfWork.
// Buổi 50: PHÁT DOMAIN EVENT sau khi SaveChanges thành công (override SaveChangesAsync).
// Buổi 52: ExecuteInTransactionAsync cho TransactionBehavior — event chờ tới khi COMMIT mới phát.
//   Thứ tự an toàn: lưu → commit → báo. Ngược lại (báo trước, lưu sau) là báo "đơn ma" khi lưu lỗi.
// Buổi 55: thêm OutboxMessages + ProcessedMessages (migration AddOutboxAndInbox). Domain event vẫn phát sau commit
//   như cũ (SignalR, trong 1 service); việc CHẮC CHẮN tới service khác thì đi qua outbox (Messaging/).
// ============================================================================
using CyberCafe.Application.Common.Interfaces;
using CyberCafe.Application.Common.Messaging;
using CyberCafe.Domain.Common;
using CyberCafe.Domain.Orders;
using CyberCafe.Domain.Products;
using CyberCafe.Infrastructure.Identity;
using CyberCafe.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CyberCafe.Infrastructure.Persistence;

/// <summary>
/// DbContext của CyberCafe. Đăng ký Scoped (1 instance / request) trong AddInfrastructure().
/// ⚠️ DbContext KHÔNG an toàn đa luồng: không dùng chung giữa 2 request, không chạy 2 query song song.
/// </summary>
// 👉 Bước 2 (b48.md): ": IUnitOfWork". 👉 Bước 6 (b50.md): tham số domainEvents (null khi test/tool tự new DbContext
// không cần phát event — vd CyberCafeApiFactory.CreateHost, dotnet ef).
public class CyberCafeDbContext(DbContextOptions<CyberCafeDbContext> options, IDomainEventPublisher? domainEvents = null)
    : DbContext(options), IUnitOfWork
{
    // Event đã lấy ra khỏi aggregate nhưng CHƯA phát (đang chờ transaction ngoài commit)
    private readonly List<IDomainEvent> _pendingEvents = [];

    // 👉 Bước 3 (b40.md): mỗi DbSet ~ 1 bảng gốc. Coffee/Tea/Cake KHÔNG cần DbSet riêng:
    // chúng nằm chung bảng Products (TPH), truy vấn bằng Products.OfType<Coffee>() hoặc p is Coffee.

    /// <summary>Thực đơn (bảng Products, TPH cho Coffee/Tea/Cake).</summary>
    public DbSet<Product> Products => this.Set<Product>();

    /// <summary>Đơn hàng (bảng Orders; thông tin khách nằm luôn trong bảng này — owned type).</summary>
    public DbSet<Order> Orders => this.Set<Order>();

    /// <summary>Dòng đơn (bảng OrderItems). Thường đi qua Order.Items; DbSet riêng để kiểm tra "món đã có trong đơn".</summary>
    public DbSet<OrderItem> OrderItems => this.Set<OrderItem>();

    // Buổi 42–47: tài khoản đăng nhập + refresh token (bảng Users, RefreshTokens)

    /// <summary>Tài khoản đăng nhập.</summary>
    public DbSet<User> Users => this.Set<User>();

    /// <summary>Refresh token đã phát (chỉ lưu hash).</summary>
    public DbSet<RefreshToken> RefreshTokens => this.Set<RefreshToken>();

    // 👉 Bước 2 (b55.md): CÙNG database với Orders → cùng transaction (Outbox pattern)

    /// <summary>Integration event chờ gửi lên RabbitMQ (Outbox).</summary>
    public DbSet<OutboxMessage> OutboxMessages => this.Set<OutboxMessage>();

    /// <summary>Message đã xử lý (idempotent consumer).</summary>
    public DbSet<ProcessedMessage> ProcessedMessages => this.Set<ProcessedMessage>();

    // 👉 Bước 6 (b50.md)
    /// <summary>
    /// Lưu thay đổi, rồi lấy domain event từ các aggregate đang theo dõi: không có transaction ngoài → phát ngay;
    /// đang trong ExecuteInTransactionAsync → giữ lại, phát sau khi commit.
    /// (SaveChangesAsync(CancellationToken) của DbContext gọi vào overload này → IUnitOfWork cũng đi qua đây.)
    /// </summary>
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        int rows = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

        // Lấy SAU khi lưu thành công: lưu lỗi → exception ở dòng trên → event không bao giờ được phát.
        // Lúc này Order.Id đã có giá trị (IDENTITY) → handler đọc được mã đơn đúng.
        this._pendingEvents.AddRange(this.TakeDomainEvents());
        if (this.Database.CurrentTransaction is null)
        {
            await this.PublishPendingEventsAsync(cancellationToken);
        }

        return rows;
    }

    // 👉 Bước 4 (b52.md)
    /// <inheritdoc />
    public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default)
    {
        // EF InMemory (test) không có transaction; đã có transaction bên ngoài → tham gia luôn, không mở lồng.
        // ⚠️ Lỗi hay gặp: gọi BeginTransaction trên InMemory → lỗi cảnh báo TransactionIgnoredWarning (mặc định ném).
        if (!this.Database.IsRelational() || this.Database.CurrentTransaction is not null)
        {
            return await work(ct);
        }

        T result;
        await using (IDbContextTransaction transaction = await this.Database.BeginTransactionAsync(ct))
        {
            try
            {
                result = await work(ct);
                await transaction.CommitAsync(ct);
            }
            catch
            {
                // Dispose không commit = ROLLBACK. Event của thay đổi đã bị hủy thì không được phát.
                this._pendingEvents.Clear();
                throw;
            }
        }

        await this.PublishPendingEventsAsync(ct); // commit xong mới báo ra ngoài
        return result;
    }

    /// <summary>Cấu hình mô hình bằng Fluent API.</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Quét assembly, áp dụng MỌI class implement IEntityTypeConfiguration<T> trong Configurations/
        // → OnModelCreating gọn, mỗi bảng 1 file cấu hình riêng.
        // ⚠️ Lỗi hay gặp: viết class cấu hình nhưng quên dòng này → EF bỏ qua, bảng sinh sai kiểu cột.
        // (Buổi 48: assembly giờ là CyberCafe.Infrastructure — Configurations chuyển CÙNG DbContext nên vẫn quét đúng.)
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CyberCafeDbContext).Assembly);
    }

    // Lấy event khỏi MỌI aggregate đang được theo dõi rồi xóa trên aggregate → lần SaveChanges sau không phát lại
    private List<IDomainEvent> TakeDomainEvents()
    {
        List<AggregateRoot> aggregates = this.ChangeTracker.Entries<AggregateRoot>()
            .Select(e => e.Entity)
            .Where(a => a.DomainEvents.Count > 0)
            .ToList();

        List<IDomainEvent> events = aggregates.SelectMany(a => a.DomainEvents).ToList();
        aggregates.ForEach(a => a.ClearDomainEvents());
        return events;
    }

    private async Task PublishPendingEventsAsync(CancellationToken ct)
    {
        if (this._pendingEvents.Count == 0 || domainEvents is null)
        {
            this._pendingEvents.Clear();
            return;
        }

        // Copy rồi xóa TRƯỚC khi phát: handler có thể gọi SaveChanges lần nữa → không phát lặp
        IDomainEvent[] batch = [.. this._pendingEvents];
        this._pendingEvents.Clear();
        await domainEvents.PublishAsync(batch, ct);
    }
}
