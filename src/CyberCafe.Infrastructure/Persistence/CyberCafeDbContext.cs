// ============================================================================
// CyberCafeDbContext.cs — "cửa ngõ" EF Core tới database CyberCafeDb (Buổi 36–41 · 48 · Code First).
// Code First: viết class C# trước (chính là class domain Product/Order...) → EF sinh bảng
// bằng migration. Không có class "entity" riêng: EF lưu thẳng domain nhờ Fluent API
// (thư mục Configurations/) — domain không phải gắn [Key], [Table]... nên vẫn là C# thuần.
// Buổi 48: chuyển từ Api/Data sang Infrastructure/Persistence. Thêm "implement IUnitOfWork" —
//   Application gọi IUnitOfWork.SaveChangesAsync() mà không biết đằng sau là EF Core.
// ============================================================================
using CyberCafe.Application.Common.Interfaces;
using CyberCafe.Domain.Orders;
using CyberCafe.Domain.Products;
using CyberCafe.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace CyberCafe.Infrastructure.Persistence;

/// <summary>
/// DbContext của CyberCafe. Đăng ký Scoped (1 instance / request) trong AddInfrastructure().
/// ⚠️ DbContext KHÔNG an toàn đa luồng: không dùng chung giữa 2 request, không chạy 2 query song song.
/// </summary>
// 👉 Bước 2 (b48.md): ": IUnitOfWork" — DbContext đã có sẵn Task<int> SaveChangesAsync(CancellationToken)
// đúng chữ ký của interface → không phải viết thêm method nào.
public class CyberCafeDbContext(DbContextOptions<CyberCafeDbContext> options) : DbContext(options), IUnitOfWork
{
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

    /// <summary>Cấu hình mô hình bằng Fluent API.</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Quét assembly, áp dụng MỌI class implement IEntityTypeConfiguration<T> trong Configurations/
        // → OnModelCreating gọn, mỗi bảng 1 file cấu hình riêng.
        // ⚠️ Lỗi hay gặp: viết class cấu hình nhưng quên dòng này → EF bỏ qua, bảng sinh sai kiểu cột.
        // (Buổi 48: assembly giờ là CyberCafe.Infrastructure — Configurations chuyển CÙNG DbContext nên vẫn quét đúng.)
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CyberCafeDbContext).Assembly);
    }
}
