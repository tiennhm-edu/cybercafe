// ============================================================================
// DependencyInjection.cs — đăng ký service của tầng Application (Buổi 48 · composition root; Buổi 51–52 · CQRS).
// Mỗi tầng tự "khai báo" service của mình qua 1 extension method:
//   builder.Services.AddApplication();                          // file này
//   builder.Services.AddInfrastructure(builder.Configuration);  // Infrastructure/DependencyInjection.cs
// Program.cs (Api) chỉ việc gọi 2 dòng trên → không phải biết từng class bên trong mỗi tầng.
// Buổi 51: hàng chục handler/validator — đăng ký TỪNG cái bằng tay thì sớm muộn cũng quên 1 cái
//   (lỗi "Chưa đăng ký handler" lúc chạy). → QUÉT assembly 1 lần bằng Reflection.
// ============================================================================
using CyberCafe.Application.Common.Behaviors;
using CyberCafe.Application.Common.Messaging;
using CyberCafe.Application.Products;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CyberCafe.Application;

/// <summary>Extension đăng ký DI cho tầng Application.</summary>
public static class DependencyInjection
{
    // Interface generic cần quét: kiểu nào trong Application implement chúng thì đăng ký Scoped
    private static readonly Type[] ScannedInterfaces = [typeof(IRequestHandler<,>), typeof(IDomainEventHandler<>), typeof(IValidator<>)];

    // 👉 Bước 10 (b48.md) · 👉 Bước 8 (b51.md)
    /// <summary>Đăng ký dispatcher, handler, validator, pipeline behavior và service còn lại.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // ⚠️ Lỗi hay gặp: đăng ký Singleton → giữ IUnitOfWork (DbContext Scoped) của request ĐẦU TIÊN
        //    → lỗi "Cannot consume scoped service from singleton" hoặc dùng chung DbContext đa luồng.
        services.AddScoped<MenuService>();

        // Dispatcher + bộ phát domain event (Buổi 50–51)
        services.AddScoped<ISender, Sender>();
        services.AddScoped<IDomainEventPublisher, DomainEventPublisher>();

        // Handler (command/query), handler domain event, validator: quét assembly này
        foreach (Type type in typeof(DependencyInjection).Assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }))
        {
            foreach (Type contract in type.GetInterfaces().Where(i => i.IsGenericType && ScannedInterfaces.Contains(i.GetGenericTypeDefinition())))
            {
                // 1 class có thể là handler của NHIỀU query (OrderQueryHandlers) → đăng ký cho từng interface
                services.AddScoped(contract, type);
            }
        }

        // 👉 Bước 1 (b52.md): pipeline behavior — THỨ TỰ ĐĂNG KÝ = THỨ TỰ BỌC (ngoài → trong):
        //   Logging (đo cả thời gian validate) → Validation (sai thì dừng, khỏi mở transaction) → Transaction → Handler
        // ⚠️ Lỗi hay gặp: đặt Transaction TRƯỚC Validation → request sai vẫn mở/đóng transaction vô ích.
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));
        return services;
    }
}
