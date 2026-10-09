// ============================================================================
// Program.cs — API Gateway (Buổi 54 · YARP reverse proxy + JWT tập trung).
// Vì sao cần gateway khi đã có nhiều service?
//   - Client (Web) chỉ biết 1 địa chỉ; thêm/tách service sau này client không phải sửa.
//   - Việc cắt ngang làm 1 lần ở cửa: xác thực, (sau này) rate limit, CORS, log truy cập.
//   - Service nội bộ không phải mở ra Internet.
// JWT: kiểm Ở ĐÂU? (bảng so sánh trong docs/sessions/b54.md)
//   /payments/* — gateway kiểm (AuthorizationPolicy "AdminOnly" trong appsettings.json) rồi CHUYỂN NGUYÊN header
//                 Authorization xuống; Payment service không có code JWT.
//   /api/*, /hubs/* — gateway chỉ chuyển tiếp; Api tự kiểm (policy chi tiết theo từng endpoint, có endpoint ẩn danh).
// ============================================================================
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// 👉 Bước 3 (b54.md): OpenTelemetry (span "gateway" là gốc của trace), health, service discovery
builder.AddServiceDefaults();

// 👉 Bước 5 (b54.md): YARP đọc bảng route/cluster từ cấu hình + hỏi service discovery địa chỉ thật của "http://api"
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();

// 👉 Bước 6 (b54.md): kiểm JWT ở gateway — CÙNG khóa, issuer, audience với Api (AppHost truyền chung parameter jwt-key)
// ⚠️ Lỗi hay gặp: gateway và Api dùng 2 khóa khác nhau → token hợp lệ ở Api nhưng gateway trả 401 cho /payments.
string jwtKey = builder.Configuration["Jwt:Key"] ?? string.Empty;
if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException("Jwt:Key phải dài tối thiểu 32 byte (AppHost truyền Jwt__Key; chạy lẻ dùng appsettings.Development.json).");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // giữ claim "role" như Api
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromSeconds(30),
            RoleClaimType = "role",
            NameClaimType = "name",
        };
    });

// Tên policy phải TRÙNG chuỗi "AuthorizationPolicy" của route trong appsettings.json
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(GatewayPolicies.AdminOnly, policy => policy.RequireAuthenticatedUser().RequireRole("Admin"));

var app = builder.Build();

// Thứ tự như Api (b47): xác thực → phân quyền → proxy. YARP chạy policy của route TRƯỚC khi chuyển tiếp.
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "CyberCafe Gateway — /api/*, /hubs/* → api · /payments/* → payment");
app.MapDefaultEndpoints();
// MapReverseProxy: mọi route trong ReverseProxy:Routes. Request không khớp route nào → 404 ngay tại gateway.
// Mặc định YARP thêm X-Forwarded-For/Proto/Host → Api đọc bằng UseForwardedHeaders (rate limit theo IP thật).
app.MapReverseProxy();

app.Run();

/// <summary>Tên policy dùng trong cấu hình route (và là "điểm neo" kiểu public cho test WebApplicationFactory).</summary>
public sealed class GatewayPolicies
{
    /// <summary>Chỉ tài khoản vai trò Admin (route /payments/*).</summary>
    public const string AdminOnly = "AdminOnly";

    private GatewayPolicies()
    {
    }
}
