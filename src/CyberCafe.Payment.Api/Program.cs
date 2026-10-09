// ============================================================================
// Program.cs — Payment service (Buổi 54 · tách service; Buổi 55 · consumer RabbitMQ, idempotent).
// So với CyberCafe.Api (Order service): KHÔNG có Clean Architecture 4 tầng — service nhỏ, 1 project là đủ.
// Microservice không bắt mọi service giống nhau bên trong; chỉ bắt chúng giữ đúng RANH GIỚI:
//   database riêng · nói chuyện qua message (CyberCafe.IntegrationEvents) · không tham chiếu code nghiệp vụ của nhau.
// Chạy: dotnet run --project src/CyberCafe.AppHost (AppHost bơm ConnectionStrings__PaymentDb + __messaging).
// ============================================================================
using CyberCafe.Payment.Api.Data;
using CyberCafe.Payment.Api.Endpoints;
using CyberCafe.Payment.Api.Messaging;
using CyberCafe.Payment.Api.Processing;
using MassTransit;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 👉 Bước 3 (b54.md): giống mọi service — OpenTelemetry, health check, service discovery, resilience
builder.AddServiceDefaults();

// 👉 Bước 2 (b54.md): database RIÊNG — connection string "PaymentDb" (tên resource trong AppHost)
builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("PaymentDb")));

// Luật thanh toán: Options pattern + kiểm tra khi khởi động (fail fast — giống JwtOptions ở b47)
builder.Services.AddOptions<PaymentRulesOptions>()
    .Bind(builder.Configuration.GetSection(PaymentRulesOptions.SectionName))
    .Validate(o => o.MaxAmount > 0 && o.SimulatedDelayMs >= 0, "Payment:MaxAmount phải > 0, SimulatedDelayMs >= 0")
    .ValidateOnStart();
// PaymentRules không giữ trạng thái theo request → Singleton; TimeProvider để test thay "đồng hồ" được (như b47)
builder.Services.AddSingleton<PaymentRules>();
builder.Services.AddSingleton(TimeProvider.System);

// 👉 Bước 4 (b55.md): MassTransit 8 (Apache-2.0) — consumer OrderPlaced; publish kết quả ngay trong consumer
builder.Services.AddMassTransit(bus =>
{
    // Queue "payment-order-placed": tiền tố service → không đụng queue của Order service
    bus.SetEndpointNameFormatter(new KebabCaseEndpointNameFormatter("payment", false));
    bus.AddConsumer<OrderPlacedConsumer>();

    string? rabbit = builder.Configuration.GetConnectionString("messaging");
    if (string.IsNullOrWhiteSpace(rabbit))
    {
        bus.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context)); // chạy lẻ / test: không có broker
    }
    else
    {
        bus.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host(new Uri(rabbit));
            // 👉 Bước 6 (b55.md): lỗi tạm thời (DB bận, trùng khóa do 2 bản song song) → thử lại giãn dần;
            // hết lượt → queue "payment-order-placed_error". Không bao giờ "nuốt" message.
            cfg.UseMessageRetry(retry => retry.Intervals(TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5)));
            cfg.ConfigureEndpoints(context);
        });
    }
});

var app = builder.Build();

// Không UseAuthentication/UseAuthorization: JWT kiểm ở gateway cho /payments/* (b54 Bước 6) — xem PaymentEndpoints.cs

// Container SQL mới → tự tạo database + bảng (AppHost đặt Database__MigrateOnStartup=true)
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using IServiceScope scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<PaymentDbContext>().Database.MigrateAsync();
}

app.MapGet("/", () => "CyberCafe Payment service — GET /payments (qua gateway, cần token Admin)");
app.MapPaymentEndpoints();
app.MapDefaultEndpoints(); // /health, /alive

app.Run();
