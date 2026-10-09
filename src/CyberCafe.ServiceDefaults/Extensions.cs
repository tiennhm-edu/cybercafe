// ============================================================================
// Extensions.cs — "mặc định của mọi service" (Buổi 54 · .NET Aspire ServiceDefaults; Buổi 55 · resilience, tracing).
// Dựa trên template `dotnet new aspire-servicedefaults`, rút gọn + chú thích cho lớp học.
// 1 hệ thống nhiều service → nếu mỗi service tự cấu hình log/trace/health theo kiểu riêng thì khi có sự cố
// không ai ghép được câu chuyện "request này đi qua đâu". Gom về 1 chỗ → ai cũng giống nhau.
//   AddServiceDefaults()   — gọi trong Program.cs của MỖI service (trước builder.Build()).
//   MapDefaultEndpoints()  — gọi sau Build(): /health (sẵn sàng nhận request?) và /alive (tiến trình còn sống?).
// ============================================================================
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

// Namespace Microsoft.Extensions.Hosting (giống template Aspire): service chỉ cần "using" có sẵn là gọi được
namespace Microsoft.Extensions.Hosting;

/// <summary>Cấu hình dùng chung cho mọi service CyberCafe chạy dưới Aspire.</summary>
public static class Extensions
{
    /// <summary>
    /// Tên ActivitySource riêng của CyberCafe (vd "CyberCafe.Outbox") — dashboard chỉ hiện trace của nguồn đã đăng ký.
    /// ⚠️ Lỗi hay gặp: tự tạo ActivitySource("...") nhưng quên AddSource → span không bao giờ lên dashboard.
    /// </summary>
    public const string ActivitySourcePrefix = "CyberCafe.";

    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    // 👉 Bước 3 (b54.md)
    /// <summary>OpenTelemetry + health check + service discovery + resilience cho mọi HttpClient.</summary>
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();

        // Service discovery: "http://api" (tên resource trong AppHost) → địa chỉ thật. AppHost bơm địa chỉ qua
        // biến môi trường services__api__http__0=http://localhost:xxxx; không có cấu hình thì giữ nguyên URL.
        builder.Services.AddServiceDiscovery();

        // 👉 Bước 6 (b55.md): MỌI HttpClient tạo bằng IHttpClientFactory đều có:
        //   retry (lỗi tạm thời: 5xx, 408, 429, timeout) · circuit breaker (service kia chết → ngừng gọi 1 lúc)
        //   · timeout từng lần thử + timeout tổng. Đây là "AddStandardResilienceHandler" của Microsoft (Polly bên dưới).
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // ⚠️ Lỗi hay gặp: retry cả POST /api/orders → lần 1 thật ra đã lưu, chỉ response bị mất → khách có 2 đơn.
            //    DisableForUnsafeHttpMethods: chỉ retry GET/HEAD/OPTIONS... (an toàn để gọi lại).
            http.AddStandardResilienceHandler(options => options.Retry.DisableForUnsafeHttpMethods());
            http.AddServiceDiscovery();
        });

        return builder;
    }

    // 👉 Bước 3 (b54.md) · 👉 Bước 7 (b55.md)
    /// <summary>Log, metric, trace theo chuẩn OpenTelemetry; có OTEL_EXPORTER_OTLP_ENDPOINT (Aspire tự đặt) thì gửi lên dashboard.</summary>
    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter("MassTransit"))
            .WithTracing(tracing => tracing
                .AddSource(builder.Environment.ApplicationName)
                // Span của chính CyberCafe (outbox dispatcher) + của MassTransit (publish/consume qua RabbitMQ).
                // MassTransit tự truyền "traceparent" trong header message → trace nối từ Api sang Payment.
                .AddSource(ActivitySourcePrefix + "*")
                .AddSource("MassTransit")
                .AddAspNetCoreInstrumentation(o =>
                    // Không ghi trace cho health check (gọi mỗi vài giây → dashboard toàn rác)
                    o.Filter = context =>
                        !context.Request.Path.StartsWithSegments(HealthEndpointPath)
                        && !context.Request.Path.StartsWithSegments(AlivenessEndpointPath))
                .AddHttpClientInstrumentation());

        // Chỉ bật exporter khi có địa chỉ (chạy dưới AppHost). Chạy test / chạy lẻ → không gửi đi đâu cả.
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return builder;
    }

    /// <summary>Health check "self" (tiến trình còn sống). Service tự thêm check DB/broker nếu cần.</summary>
    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);
        return builder;
    }

    /// <summary>
    /// /health: mọi check đều Healthy (AppHost dùng để WaitFor). /alive: chỉ check "live".
    /// Chỉ bật ở Development — endpoint này lộ thông tin nội bộ, môi trường thật cần bảo vệ trước khi mở.
    /// </summary>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapHealthChecks(HealthEndpointPath);
            app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains("live"),
            });
        }

        return app;
    }
}
