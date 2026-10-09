// ============================================================================
// Program.cs — điểm khởi động CyberCafe.Api (Buổi 32–41).
//   Buổi 32–35: Web API controllers + OpenAPI/Scalar để thử API.
//   Buổi 33–34: SignalR OrderHub — đơn mới realtime cho quầy barista.
//   Buổi 36–41: EF Core SQL Server (DbContext Scoped), báo cáo doanh thu (stored procedure).
// Vẫn 2 phần như Web: (1) builder.Services... đăng ký DI; (2) app.Use/Map... cấu hình pipeline.
// Chạy: docker compose up -d sqlserver → dotnet ef database update → dotnet run --project src/CyberCafe.Api
//       → http://localhost:5180/scalar/v1
// ============================================================================
using System.Text.Json.Serialization;
using CyberCafe.Api.Data;
using CyberCafe.Api.Realtime;
using CyberCafe.Api.Reports;
using CyberCafe.Contracts.Realtime;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ===== 1. Đăng ký service =====

// 👉 Bước 1 (b40.md): controllers + JSON enum dạng chuỗi ("Pending" thay vì 0) — khớp với Web client.
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// OpenAPI (.NET 9+ có sẵn, không cần Swashbuckle): sinh tài liệu tại /openapi/v1.json.
// Scalar (bên dưới) đọc file đó để vẽ giao diện thử API.
builder.Services.AddOpenApi();

// 👉 Bước 3 (b40.md): DbContext. AddDbContext mặc định SCOPED = 1 instance / request.
// Connection string đọc từ "ConnectionStrings:CyberCafe" (appsettings.Development.json / user-secrets / biến môi trường).
// ⚠️ Lỗi hay gặp: đăng ký DbContext là Singleton → nhiều request dùng chung 1 DbContext → lỗi đa luồng.
builder.Services.AddDbContext<CyberCafeDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("CyberCafe")));

// 👉 Bước 9 (b40.md): SignalR. JSON của hub cũng đổi enum sang chuỗi → giống REST.
builder.Services.AddSignalR()
    .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddScoped<IOrderNotifier, SignalROrderNotifier>();

// Báo cáo doanh thu (SP trên SQL Server, LINQ trên provider khác)
builder.Services.AddScoped<RevenueReportService>();

// Không cần CORS: Web là Blazor SERVER → gọi API và hub từ máy chủ Web (server-to-server),
// trình duyệt không gọi thẳng API. CORS chỉ cần khi JavaScript trên trình duyệt gọi sang origin khác.

var app = builder.Build();

// ===== 2. Pipeline =====
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();                 // /openapi/v1.json
    app.MapScalarApiReference();      // /scalar/v1 — giao diện thử API
    app.MapGet("/", () => Results.Redirect("/scalar/v1")).ExcludeFromDescription();
}

// Không UseHttpsRedirection: Web gọi API qua http://localhost:5180 trong môi trường dev;
// redirect sang https sẽ làm POST/PUT bị đổi thành GET ở một số client. Production đặt sau reverse proxy lo HTTPS.

app.MapControllers();
app.MapHub<OrderHub>(OrderHubContract.Path); // ws://localhost:5180/hubs/orders

app.Run();

// Cho phép project test dùng WebApplicationFactory<Program> (Program sinh từ top-level statements là internal).
/// <summary>Điểm vào của ứng dụng (khai báo partial để test tích hợp truy cập được).</summary>
public partial class Program;
