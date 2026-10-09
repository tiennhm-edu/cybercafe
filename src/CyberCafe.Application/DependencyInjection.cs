// ============================================================================
// DependencyInjection.cs — đăng ký service của tầng Application (Buổi 48 · composition root).
// Mỗi tầng tự "khai báo" service của mình qua 1 extension method:
//   builder.Services.AddApplication();                          // file này
//   builder.Services.AddInfrastructure(builder.Configuration);  // Infrastructure/DependencyInjection.cs
// Program.cs (Api) chỉ việc gọi 2 dòng trên → không phải biết từng class bên trong mỗi tầng.
// ============================================================================
using CyberCafe.Application.Orders;
using CyberCafe.Application.Products;
using Microsoft.Extensions.DependencyInjection;

namespace CyberCafe.Application;

/// <summary>Extension đăng ký DI cho tầng Application.</summary>
public static class DependencyInjection
{
    // 👉 Bước 10 (b48.md)
    /// <summary>Đăng ký use case service (Scoped: cùng vòng đời với DbContext — 1 instance / request).</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // ⚠️ Lỗi hay gặp: đăng ký Singleton → service giữ IUnitOfWork (DbContext Scoped) của request ĐẦU TIÊN
        //    → lỗi "Cannot consume scoped service from singleton" (Development) hoặc dùng chung DbContext đa luồng.
        services.AddScoped<MenuService>();
        services.AddScoped<OrderService>();
        return services;
    }
}
