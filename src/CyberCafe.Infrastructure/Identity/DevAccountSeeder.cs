// ============================================================================
// DevAccountSeeder.cs — tạo 3 tài khoản mẫu cho máy DEV (Buổi 42–47 · 48: chuyển sang Infrastructure/Identity).
// Vì sao KHÔNG dùng HasData như thực đơn?
//   HasData nằm trong migration → chạy cả ở production → server thật có sẵn tài khoản admin
//   với mật khẩu ai cũng biết. Ở đây chỉ seed khi cấu hình "Seed:DevAccounts" = true
//   (bật trong appsettings.Development.json và trong test), mật khẩu đọc từ cấu hình.
// Email dùng đuôi .local (không phải domain thật), mật khẩu là giá trị GIẢ chỉ để học.
// ============================================================================
using CyberCafe.Contracts.Auth;
using CyberCafe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CyberCafe.Infrastructure.Identity;

/// <summary>Seed tài khoản admin / barista / customer cho môi trường phát triển.</summary>
public static class DevAccountSeeder
{
    /// <summary>Email tài khoản Admin mẫu.</summary>
    public const string AdminEmail = "admin@cybercafe.local";

    /// <summary>Email tài khoản Barista mẫu.</summary>
    public const string BaristaEmail = "barista@cybercafe.local";

    /// <summary>Email tài khoản Customer mẫu.</summary>
    public const string CustomerEmail = "customer@cybercafe.local";

    /// <summary>Tạo tài khoản nếu chưa có (chạy lại nhiều lần không trùng).</summary>
    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using IServiceScope scope = services.CreateScope(); // DbContext là Scoped → phải tạo scope riêng lúc khởi động
        IConfiguration config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        ILogger logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DevAccountSeeder));

        if (!config.GetValue<bool>("Seed:DevAccounts"))
        {
            return;
        }

        string password = config["Seed:DevPassword"] ?? "Dev@12345";
        CyberCafeDbContext db = scope.ServiceProvider.GetRequiredService<CyberCafeDbContext>();
        IPasswordHasher hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        try
        {
            (string Email, string Name, string? Phone, UserRole Role)[] accounts =
            [
                (AdminEmail, "Quản trị CyberCafe", null, UserRole.Admin),
                (BaristaEmail, "Barista Ca Sáng", null, UserRole.Barista),
                (CustomerEmail, "Khách Mẫu", "0901234567", UserRole.Customer),
            ];

            foreach (var account in accounts)
            {
                if (await db.Users.AnyAsync(u => u.Email == account.Email, ct))
                {
                    continue;
                }

                db.Users.Add(new User
                {
                    Email = account.Email,
                    FullName = account.Name,
                    PhoneNumber = account.Phone,
                    Role = account.Role,
                    PasswordHash = hasher.Hash(password),
                    CreatedAt = DateTime.UtcNow,
                });
            }

            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Hay gặp nhất: chưa chạy "dotnet ef database update" → bảng Users chưa có. App vẫn chạy tiếp.
            logger.LogWarning(ex, "Không seed được tài khoản dev — đã chạy 'dotnet ef database update' chưa?");
        }
    }
}
