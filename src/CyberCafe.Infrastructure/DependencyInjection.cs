// ============================================================================
// DependencyInjection.cs — đăng ký mọi "adapter" hạ tầng (Buổi 48 · composition root).
// Những dòng này trước nằm rải trong Program.cs (b47): AddDbContext, JwtOptions, BCrypt, Redis...
// Gom về đây → Program.cs chỉ gọi builder.Services.AddInfrastructure(builder.Configuration).
// Mỗi dòng "AddScoped<IPort, Adapter>()" chính là chỗ NỐI interface của Application với cài đặt cụ thể.
// ============================================================================
using System.Text;
using CyberCafe.Application.Auth;
using CyberCafe.Application.Caching;
using CyberCafe.Application.Common.Interfaces;
using CyberCafe.Application.Orders;
using CyberCafe.Application.Products;
using CyberCafe.Application.Reports;
using CyberCafe.Infrastructure.Caching;
using CyberCafe.Infrastructure.Identity;
using CyberCafe.Infrastructure.Persistence;
using CyberCafe.Infrastructure.Persistence.Repositories;
using CyberCafe.Infrastructure.Realtime;
using CyberCafe.Infrastructure.Reports;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CyberCafe.Infrastructure;

/// <summary>Extension đăng ký DI cho tầng Infrastructure.</summary>
public static class DependencyInjection
{
    // 👉 Bước 10 (b48.md)
    /// <summary>EF Core + repository, Identity (JWT/BCrypt), cache, báo cáo.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // TimeProvider: "đồng hồ" có thể thay bằng đồng hồ giả trong test (token hết hạn, refresh hết hạn...)
        services.TryAddSingleton(TimeProvider.System);

        // ----- Persistence -----
        // 👉 Bước 3 (b40.md): AddDbContext mặc định SCOPED = 1 instance / request.
        // Connection string "ConnectionStrings:CyberCafe" (appsettings.Development.json / user-secrets / biến môi trường).
        // ⚠️ Lỗi hay gặp: đăng ký DbContext là Singleton → nhiều request dùng chung 1 DbContext → lỗi đa luồng.
        services.AddDbContext<CyberCafeDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("CyberCafe")));
        // Cùng 1 DbContext của request cho cả repository lẫn IUnitOfWork → SaveChanges lưu đúng thứ repository đã Add.
        // ⚠️ Lỗi hay gặp: AddScoped<IUnitOfWork, CyberCafeDbContext>() → DI tạo DbContext THỨ HAI, SaveChanges không lưu gì.
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<CyberCafeDbContext>());
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IRevenueReportService, RevenueReportService>();

        // ----- Identity (Buổi 42–47) -----
        // 👉 Bước 1 (b47.md): Options pattern + kiểm tra NGAY khi khởi động (fail fast) — thiếu key thì không chạy.
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => Encoding.UTF8.GetByteCount(o.Key) >= 32,
                "Jwt:Key phải dài tối thiểu 32 byte (đặt bằng user-secrets hoặc biến môi trường Jwt__Key).")
            .ValidateOnStart();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<IAuthService, AuthService>();

        // ----- Cache (Buổi 42–47) -----
        // Redis khi bật cờ "Redis:Enabled", ngược lại dùng cache trong RAM (cùng interface IDistributedCache
        // → MenuCache không cần biết đang chạy loại nào; test và máy không có Docker vẫn chạy được).
        string? redis = configuration.GetConnectionString("Redis");
        if (configuration.GetValue<bool>("Redis:Enabled") && !string.IsNullOrWhiteSpace(redis))
        {
            services.AddStackExchangeRedisCache(o =>
            {
                o.Configuration = redis;
                o.InstanceName = "cybercafe:"; // tiền tố key — nhiều app dùng chung 1 Redis không đụng nhau
            });
        }
        else
        {
            services.AddDistributedMemoryCache();
        }

        services.AddSingleton<IMenuCache, MenuCache>();
        return services;
    }

    // 👉 Bước 9 (b48.md)
    /// <summary>
    /// Nối IOrderNotifier với hub SignalR cụ thể. Tách riêng vì kiểu hub (OrderHub) nằm ở Api —
    /// Program.cs gọi: builder.Services.AddOrderNotifier&lt;OrderHub&gt;().
    /// </summary>
    public static IServiceCollection AddOrderNotifier<THub>(this IServiceCollection services)
        where THub : Hub<IOrderClient> =>
        services.AddScoped<IOrderNotifier, SignalROrderNotifier<THub>>();
}
