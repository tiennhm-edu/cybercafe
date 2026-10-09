// ============================================================================
// Program.cs — điểm khởi động của ứng dụng (Buổi 23 · Hosting, DI, middleware;
//              Buổi 24–31 đăng ký thêm CartState;
//              Buổi 32–41: dữ liệu đi qua CyberCafe.Api bằng typed HttpClient + SignalR).
// Gồm 2 phần:
//   1) builder.Services...  : ĐĂNG KÝ service vào DI container (trước Build()).
//   2) app.Use.../app.Map... : cấu hình pipeline xử lý request (sau Build()).
// ============================================================================
using CyberCafe.Web.Components;
using CyberCafe.Web.Services;
using CyberCafe.Web.State;

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
builder.Services.AddHttpClient<MenuApiClient>(client =>
{
    client.BaseAddress = apiBaseUrl;
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddHttpClient<OrderApiClient>(client =>
{
    client.BaseAddress = apiBaseUrl;
    client.Timeout = TimeSpan.FromSeconds(10);
});

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

// Chống giả mạo request (CSRF) — bắt buộc cho form (EditForm) trong Blazor Web App
app.UseAntiforgery();

// Phục vụ file tĩnh trong wwwroot (CSS, ảnh...) kèm fingerprint để cache tốt
app.MapStaticAssets();
// App là component gốc (App.razor); bật endpoint SignalR cho Interactive Server
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
