// ============================================================================
// Program.cs — điểm khởi động của ứng dụng (Buổi 23 · Hosting, DI, middleware;
//              Buổi 24–31 đăng ký thêm CartState;
//              Buổi 32–41: dữ liệu đi qua CyberCafe.Api bằng typed HttpClient + SignalR;
//              Buổi 42–47: đăng nhập JWT — AuthSession, AuthenticationStateProvider, DelegatingHandler).
// Gồm 2 phần:
//   1) builder.Services...  : ĐĂNG KÝ service vào DI container (trước Build()).
//   2) app.Use.../app.Map... : cấu hình pipeline xử lý request (sau Build()).
// ============================================================================
using CyberCafe.Web.Components;
using CyberCafe.Web.Services;
using CyberCafe.Web.Services.Auth;
using CyberCafe.Web.State;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;

var builder = WebApplication.CreateBuilder(args);

// Blazor Web App + Interactive Server render mode (SignalR circuit)
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// 3 vòng đời (lifetime) cần nhớ:
//   - Singleton: 1 instance duy nhất cho cả app, mọi người dùng dùng chung.
//   - Scoped   : Blazor Server → 1 instance cho mỗi circuit (mỗi tab trình duyệt).
//   - Transient: tạo mới mỗi lần được inject.

// 👉 Bước 10 (b40.md): địa chỉ Api đọc từ cấu hình (appsettings.json → "ApiBaseUrl"), không viết cứng trong code.
// ⚠️ Lỗi hay gặp: thiếu dấu "/" cuối BaseAddress → "api/products" ghép thành ".../5180api/products".
Uri apiBaseUrl = new((builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5180").TrimEnd('/') + "/");

// Buổi 23–31: MenuService + OrderStore (in-memory, Singleton) → nay thay bằng TYPED HTTPCLIENT.
// AddHttpClient<T>: IHttpClientFactory tạo HttpClient cho T (đăng ký Transient), tái sử dụng HttpMessageHandler
// bên dưới → không cạn socket, tự làm mới DNS. Component chỉ cần @inject MenuApiClient.
// Buổi 42–47: .AddHttpMessageHandler<BearerTokenHandler>() chèn handler gắn token vào đường ống của client.
builder.Services.AddTransient<BearerTokenHandler>(); // handler PHẢI là Transient (factory tự quản vòng đời)
builder.Services.AddHttpClient<MenuApiClient>(client =>
{
    client.BaseAddress = apiBaseUrl;
    client.Timeout = TimeSpan.FromSeconds(10);
}).AddHttpMessageHandler<BearerTokenHandler>();
builder.Services.AddHttpClient<OrderApiClient>(client =>
{
    client.BaseAddress = apiBaseUrl;
    client.Timeout = TimeSpan.FromSeconds(10);
}).AddHttpMessageHandler<BearerTokenHandler>();
// AuthApiClient KHÔNG gắn BearerTokenHandler (login/refresh không dùng access token — tránh vòng lặp refresh)
builder.Services.AddHttpClient<AuthApiClient>(client =>
{
    client.BaseAddress = apiBaseUrl;
    client.Timeout = TimeSpan.FromSeconds(10);
});

// ===== Buổi 42–47: đăng nhập =====
// 👉 Bước 13 (b47.md): phiên đăng nhập theo CIRCUIT (Scoped) + nơi lưu (sessionStorage mã hóa)
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ITokenStorage, ProtectedSessionTokenStorage>();
builder.Services.AddScoped<AuthSession>();
// 1 instance, lấy được bằng 2 kiểu: kiểu cụ thể và kiểu AuthenticationStateProvider (cho AuthorizeView...).
// ⚠️ Lỗi hay gặp: AddScoped<AuthenticationStateProvider, JwtAuthenticationStateProvider>() + AddScoped<JwtAuthenticationStateProvider>()
//    → 2 instance khác nhau, đăng nhập xong giao diện không đổi.
builder.Services.AddScoped<JwtAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<JwtAuthenticationStateProvider>());
// Task<AuthenticationState> được truyền xuống MỌI component dưới dạng cascading parameter
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorization();
// Trang [Authorize] được kiểm tra trong circuit (AuthorizeRouteView), không chặn ở middleware HTTP
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, BlazorAuthorizationResultHandler>();

// Buổi 33–34: nhà máy tạo HubConnection tới Api/hubs/orders (Singleton vì chỉ giữ URL, không giữ kết nối)
builder.Services.AddSingleton(new OrderHubConnectionFactory(apiBaseUrl));

// Giỏ hàng: mỗi circuit (tab trình duyệt) 1 giỏ riêng → Scoped
// Mọi component trong CÙNG 1 tab (Menu, CartSummary, CartPage, Checkout) nhận CÙNG 1 CartState
// → dùng chung giỏ. Tab khác / người dùng khác có giỏ riêng.
// ⚠️ Lỗi hay gặp: đăng ký CartState là Singleton → MỌI người dùng chung 1 giỏ hàng!
//    Còn Transient → mỗi component 1 giỏ riêng, thêm món ở Menu mà CartSummary vẫn 0.
builder.Services.AddScoped<CartState>();

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

// Buổi 42–47: trang có [Authorize] mang metadata phân quyền → cần middleware Authorization
// (kết quả ở tầng HTTP được BlazorAuthorizationResultHandler cho qua; kiểm tra thật trong circuit).
app.UseAuthorization();

// Chống giả mạo request (CSRF) — bắt buộc cho form (EditForm) trong Blazor Web App
app.UseAntiforgery();

// Phục vụ file tĩnh trong wwwroot (CSS, ảnh...) kèm fingerprint để cache tốt
app.MapStaticAssets();
// App là component gốc (App.razor); bật endpoint SignalR cho Interactive Server
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
