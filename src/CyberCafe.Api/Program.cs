// ============================================================================
// Program.cs — điểm khởi động CyberCafe.Api.
//   Buổi 32–35: Web API controllers + OpenAPI/Scalar để thử API.
//   Buổi 33–34: SignalR OrderHub — đơn mới realtime cho quầy barista.
//   Buổi 36–41: EF Core SQL Server (DbContext Scoped), báo cáo doanh thu (stored procedure).
//   Buổi 42–47: JWT + policy, middleware (correlation id, request log), exception handler → ProblemDetails,
//               Redis cache, rate limiting, seed tài khoản dev.
// Vẫn 2 phần như Web: (1) builder.Services... đăng ký DI; (2) app.Use/Map... cấu hình pipeline.
// Chạy: docker compose up -d → dotnet ef database update → dotnet run --project src/CyberCafe.Api
//       → http://localhost:5180/scalar/v1
// ============================================================================
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using CyberCafe.Api.Auth;
using CyberCafe.Api.Caching;
using CyberCafe.Api.Controllers;
using CyberCafe.Api.Data;
using CyberCafe.Api.Errors;
using CyberCafe.Api.Middleware;
using CyberCafe.Api.Realtime;
using CyberCafe.Api.Reports;
using CyberCafe.Contracts.Auth;
using CyberCafe.Contracts.Realtime;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ===== 1. Đăng ký service =====

// 👉 Bước 1 (b40.md): controllers + JSON enum dạng chuỗi ("Pending" thay vì 0) — khớp với Web client.
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// OpenAPI (.NET 9+ có sẵn, không cần Swashbuckle): sinh tài liệu tại /openapi/v1.json.
// Buổi 42–47: khai báo JWT Bearer → Scalar có ô nhập token.
builder.Services.AddOpenApi(o => o.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

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

// ===== Buổi 42–47: Authentication (bạn là ai?) + Authorization (bạn được làm gì?) =====
// TimeProvider: "đồng hồ" có thể thay bằng đồng hồ giả trong test (token hết hạn, refresh hết hạn...)
builder.Services.AddSingleton(TimeProvider.System);

// 👉 Bước 1 (b47.md): Options pattern + kiểm tra NGAY khi khởi động (fail fast) — thiếu key thì không chạy.
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .Validate(o => Encoding.UTF8.GetByteCount(o.Key) >= 32,
        "Jwt:Key phải dài tối thiểu 32 byte (đặt bằng user-secrets hoặc biến môi trường Jwt__Key).")
    .ValidateOnStart();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddScoped<AuthService>();

// JwtBearer kiểm tra "Authorization: Bearer <token>" ở MỖI request (không tra DB: chỉ kiểm chữ ký + hạn).
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();
// Cấu hình JwtBearer TRỄ (đọc JwtOptions qua DI lúc chạy) thay vì đọc config ngay tại đây →
// test đổi Jwt:Key bằng UseSetting vẫn có hiệu lực.
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
    {
        JwtOptions jwt = jwtOptions.Value;
        options.MapInboundClaims = false; // giữ nguyên tên claim "sub", "role" (không đổi sang URI dài của .NET)
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,                    // hết hạn → 401
            ClockSkew = TimeSpan.FromSeconds(30),       // mặc định 5 phút — quá rộng cho token 15 phút
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            NameClaimType = AppClaimTypes.Name,         // User.Identity.Name
            RoleClaimType = AppClaimTypes.Role,         // User.IsInRole / [Authorize(Roles = ...)]
        };
        // 👉 Bước 12 (b47.md): WebSocket của SignalR không gửi được header → client gửi ?access_token=...
        // Chỉ chấp nhận cách này cho đường dẫn hub (API thường vẫn bắt buộc header).
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                string? accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.Request.Path.StartsWithSegments(OrderHubContract.Path))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            },
        };
    });
builder.Services.AddCyberCafePolicies(); // ManageMenu / ProcessOrders / PlaceOrders (Auth/Policies.cs)

// ===== Buổi 42–47: lỗi → ProblemDetails thống nhất =====
// AddProblemDetails: 404/405/500... đều trả application/problem+json. CustomizeProblemDetails chạy cho MỌI
// ProblemDetails (cả ValidationProblem của [ApiController]) → gắn correlationId để tra log.
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
    ctx.ProblemDetails.Extensions["correlationId"] = ctx.HttpContext.Items[CorrelationIdMiddleware.ItemKey]);
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

// ===== Buổi 42–47: Cache =====
// Redis khi bật cờ "Redis:Enabled", ngược lại dùng cache trong RAM (cùng interface IDistributedCache
// → MenuCache không cần biết đang chạy loại nào; test và máy không có Docker vẫn chạy được).
string? redis = builder.Configuration.GetConnectionString("Redis");
if (builder.Configuration.GetValue<bool>("Redis:Enabled") && !string.IsNullOrWhiteSpace(redis))
{
    builder.Services.AddStackExchangeRedisCache(o =>
    {
        o.Configuration = redis;
        o.InstanceName = "cybercafe:"; // tiền tố key — nhiều app dùng chung 1 Redis không đụng nhau
    });
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

builder.Services.AddSingleton<MenuCache>();

// ===== Buổi 42–47: Rate limiting (có sẵn trong ASP.NET Core) =====
// Fixed window: mỗi IP tối đa N request / 1 phút cho đăng nhập + đăng ký. Quá → 429 Too Many Requests.
int loginPermit = builder.Configuration.GetValue("RateLimiting:LoginPermitLimit", 5);
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy(AuthController.LoginRateLimit, http =>
        // Partition theo IP: mỗi IP 1 "xô" riêng. (Sau reverse proxy cần UseForwardedHeaders để có IP thật.)
        RateLimitPartition.GetFixedWindowLimiter(
            http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = loginPermit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0, // không xếp hàng chờ: từ chối luôn
            }));
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new()
        {
            HttpContext = context.HttpContext,
            ProblemDetails = { Status = 429, Title = "Thử quá nhiều lần", Detail = "Vui lòng đợi 1 phút rồi thử lại." },
        });
    };
});

// Không cần CORS: Web là Blazor SERVER → gọi API và hub từ máy chủ Web (server-to-server),
// trình duyệt không gọi thẳng API. CORS chỉ cần khi JavaScript trên trình duyệt gọi sang origin khác.

var app = builder.Build();

// ===== 2. Pipeline — THỨ TỰ = thứ tự request đi qua (response đi ngược lại) =====

// 👉 Bước 6 (b47.md)
// (1) Correlation id ngoài cùng: mọi thứ phía sau (kể cả log lỗi) đều có mã.
app.UseMiddleware<CorrelationIdMiddleware>();
// (2) Log request: bọc ngoài exception handler → log được cả request lỗi (đã thành 4xx/500).
app.UseMiddleware<RequestLoggingMiddleware>();
// (3) Bắt exception → DomainExceptionHandler (400/401/409) hoặc 500 ProblemDetails.
// ⚠️ Lỗi hay gặp: đặt UseExceptionHandler ở CUỐI → không bắt được lỗi của các middleware đứng trước nó.
app.UseExceptionHandler();
// (4) Response lỗi không có body (404 sai URL, 405...) → cũng thành ProblemDetails.
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();                 // /openapi/v1.json
    // /scalar/v1 — ô "Authentication" dùng scheme Bearer khai báo ở trên
    app.MapScalarApiReference(o => o.AddPreferredSecuritySchemes(BearerSecuritySchemeTransformer.SchemeName));
    app.MapGet("/", () => Results.Redirect("/scalar/v1")).ExcludeFromDescription();
}

// Không UseHttpsRedirection: Web gọi API qua http://localhost:5180 trong môi trường dev;
// redirect sang https sẽ làm POST/PUT bị đổi thành GET ở một số client. Production đặt sau reverse proxy lo HTTPS.

// (5) Xác thực RỒI mới phân quyền.
// ⚠️ Lỗi hay gặp: UseAuthorization đứng TRƯỚC UseAuthentication → User luôn rỗng → mọi request 401.
app.UseAuthentication();
app.UseAuthorization();
// (6) Rate limiter sau routing (WebApplication tự thêm UseRouting ở đầu) để đọc được [EnableRateLimiting] của action.
app.UseRateLimiter();

app.MapControllers();
app.MapHub<OrderHub>(OrderHubContract.Path); // ws://localhost:5180/hubs/orders

// Seed tài khoản dev (chỉ khi Seed:DevAccounts = true — xem DevAccountSeeder.cs)
await DevAccountSeeder.SeedAsync(app.Services);

app.Run();

// Cho phép project test dùng WebApplicationFactory<Program> (Program sinh từ top-level statements là internal).
/// <summary>Điểm vào của ứng dụng (khai báo partial để test tích hợp truy cập được).</summary>
public partial class Program;
