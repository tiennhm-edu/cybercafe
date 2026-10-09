// ============================================================================
// UserConfiguration.cs — bảng Users + RefreshTokens (Buổi 42–47 · 48).
// Unique index trên Email và TokenHash: database tự chặn trùng, kể cả khi 2 request đăng ký
// cùng lúc lọt qua bước kiểm tra AnyAsync trong code (race condition).
// ============================================================================
using CyberCafe.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberCafe.Infrastructure.Persistence.Configurations;

/// <summary>Cấu hình bảng Users.</summary>
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.Property(u => u.Email).HasMaxLength(256).IsRequired();
        builder.HasIndex(u => u.Email).IsUnique();

        // Chuỗi BCrypt dài cố định 60 ký tự; để 100 cho thuật toán khác sau này
        builder.Property(u => u.PasswordHash).HasMaxLength(100).IsRequired();
        builder.Property(u => u.FullName).HasMaxLength(50).IsRequired();
        builder.Property(u => u.PhoneNumber).HasMaxLength(11);

        // Enum → chuỗi giống Orders.Status: cột Role = 'Admin' dễ đọc khi tra DB
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);

        // 1 User – n RefreshToken; xóa user → xóa luôn token
        builder.HasMany(u => u.RefreshTokens)
            .WithOne(t => t.User)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Cấu hình bảng RefreshTokens.</summary>
public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        // SHA-256 → Base64 = 44 ký tự
        builder.Property(t => t.TokenHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.Property(t => t.ReplacedByTokenHash).HasMaxLength(64);
    }
}
