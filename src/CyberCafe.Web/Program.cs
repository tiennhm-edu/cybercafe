// ============================================================================
// Program.cs — điểm khởi động của ứng dụng (Buổi 23 · Hosting, DI, middleware).
// Gồm 2 phần:
//   1) builder.Services...  : ĐĂNG KÝ service vào DI container (trước Build()).
//   2) app.Use.../app.Map... : cấu hình pipeline xử lý request (sau Build()).
// ============================================================================
using CyberCafe.Web.Components;
using CyberCafe.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Blazor Web App + Interactive Server render mode (SignalR circuit)
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Dependency Injection: dữ liệu menu dùng chung cho toàn app → Singleton
// 3 vòng đời (lifetime) cần nhớ:
//   - Singleton: 1 instance duy nhất cho cả app, mọi người dùng dùng chung.
//   - Scoped   : Blazor Server → 1 instance cho mỗi circuit (mỗi tab trình duyệt).
//   - Transient: tạo mới mỗi lần được inject.
builder.Services.AddSingleton<MenuService>();

var app = builder.Build();

// Môi trường Production: lỗi → trang /Error thân thiện; HSTS buộc trình duyệt dùng HTTPS.
// Development thì hiện trang lỗi chi tiết để debug.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
// Request trả về 404 (vd gõ sai URL) → render lại bằng trang /not-found (NotFound.razor)
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

// Chống giả mạo request (CSRF) — bắt buộc cho form (EditForm) trong Blazor Web App
app.UseAntiforgery();

// Phục vụ file tĩnh trong wwwroot (CSS, ảnh...) kèm fingerprint để cache tốt
app.MapStaticAssets();
// App là component gốc (App.razor); bật endpoint SignalR cho Interactive Server
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
