// ============================================================================
// AppHost.cs — khai báo TOÀN BỘ hệ thống CyberCafe bằng C# (Buổi 54 · .NET Aspire; Buổi 55 · RabbitMQ).
// Thay cho docker-compose.yml + 4 terminal "dotnet run": 1 lệnh chạy hết, đúng thứ tự, tự nối cấu hình.
//
//            ┌──────── Web (Blazor) ────────┐   chỉ biết "gateway"
//            ▼                              │
//         Gateway (YARP) ── /api, /hubs ──► Api (Order/Menu/Identity) ──► SQL: CyberCafeDb, Redis
//                       └── /payments ───► Payment.Api ──────────────────► SQL: CyberCafePayments
//                                Api ◄──── RabbitMQ (messaging) ────► Payment.Api
//
// Mỗi WithReference(x) = "service này cần x" → Aspire bơm biến môi trường:
//   database/redis/rabbit → ConnectionStrings__<tên>     project → services__<tên>__http__0 (service discovery)
// WaitFor(x): chỉ khởi động khi x đã healthy (SQL nhận kết nối, RabbitMQ sẵn sàng...) → hết lỗi "chạy Api trước SQL".
// Chạy: dotnet run --project src/CyberCafe.AppHost   (cần Docker Desktop; dashboard: http://localhost:15080)
// ============================================================================

var builder = DistributedApplication.CreateBuilder(args);

// 👉 Bước 1 (b54.md): parameter = cấu hình bí mật/thay đổi theo máy. Giá trị GIẢ cho dev nằm trong appsettings.json
// (section "Parameters"); máy thật: dotnet user-secrets set "Parameters:jwt-key" "<khóa thật>" (project AppHost).
var sqlPassword = builder.AddParameter("sql-password", secret: true);
var jwtKey = builder.AddParameter("jwt-key", secret: true);

// ----- Hạ tầng (container) -----
// SQL Server: 1 container, 2 database — mỗi service 1 database riêng ("database per service").
// Cổng host cố định 14330 để mở bằng SSMS/Azure Data Studio (khác 1434 của docker-compose → chạy song song được).
// WithDataVolume: dữ liệu nằm trong volume có tên → tắt AppHost bật lại không mất đơn hàng.
// ⚠️ Lỗi hay gặp: đổi sql-password sau khi volume đã tạo → SQL Server giữ mật khẩu CŨ → Api không đăng nhập được.
//    Cách gỡ: docker volume rm cybercafe-b55-sql (mất dữ liệu dev) — xem README, mục dọn dẹp.
var sql = builder.AddSqlServer("sql", sqlPassword, port: 14330)
    .WithImageTag("2022-latest")           // cùng image với docker-compose (b40) → không phải tải thêm
    .WithDataVolume("cybercafe-b55-sql");
var cafeDb = sql.AddDatabase("CyberCafe", databaseName: "CyberCafeDb");          // → ConnectionStrings__CyberCafe
var paymentDb = sql.AddDatabase("PaymentDb", databaseName: "CyberCafePayments"); // → ConnectionStrings__PaymentDb

// Redis: cache thực đơn (b47). Không cần volume — cache mất thì đọc lại từ DB.
var redis = builder.AddRedis("Redis");                                           // → ConnectionStrings__Redis

// 👉 Bước 3 (b55.md): RabbitMQ — message broker giữa Api và Payment. Management UI (xem exchange/queue) mở từ dashboard.
var rabbit = builder.AddRabbitMQ("messaging")                                     // → ConnectionStrings__messaging
    .WithManagementPlugin();

// ----- Service (project .NET) -----
// 👉 Bước 2 (b54.md)
var api = builder.AddProject<Projects.CyberCafe_Api>("api", launchProfileName: "http")
    .WithReference(cafeDb).WaitFor(cafeDb)
    .WithReference(redis).WaitFor(redis)
    .WithReference(rabbit).WaitFor(rabbit)
    .WithEnvironment("Jwt__Key", jwtKey)
    .WithEnvironment("Redis__Enabled", "true")
    .WithEnvironment("Payments__Flow", "Messaging")         // thanh toán qua Payment service (b55), không Pay ngay
    .WithEnvironment("Database__MigrateOnStartup", "true")  // container mới → tự tạo bảng
    .WithHttpHealthCheck("/health");

var payment = builder.AddProject<Projects.CyberCafe_Payment_Api>("payment")
    .WithReference(paymentDb).WaitFor(paymentDb)
    .WithReference(rabbit).WaitFor(rabbit)
    .WithEnvironment("Database__MigrateOnStartup", "true")
    .WithHttpHealthCheck("/health");

// Gateway: biết địa chỉ api + payment qua service discovery; cùng khóa JWT với Api để kiểm token cho /payments/*
var gateway = builder.AddProject<Projects.CyberCafe_Gateway>("gateway")
    .WithReference(api).WaitFor(api)
    .WithReference(payment).WaitFor(payment)
    .WithEnvironment("Jwt__Key", jwtKey)
    .WithExternalHttpEndpoints();

// 👉 Bước 7 (b54.md): Web CHỈ nói chuyện với gateway.
//   ApiBaseUrl = "http://gateway/"  → HttpClient (IHttpClientFactory + AddServiceDiscovery) tự đổi ra địa chỉ thật.
//   HubBaseUrl = địa chỉ THẬT của gateway → HubConnection của SignalR KHÔNG đi qua IHttpClientFactory nên không
//                hiểu tên "gateway"; Aspire điền URL lúc chạy (GetEndpoint).
// ⚠️ Lỗi hay gặp: đặt HubBaseUrl = "http://gateway" → SignalR báo "No such host is known".
builder.AddProject<Projects.CyberCafe_Web>("web", launchProfileName: "http")
    .WithReference(gateway).WaitFor(gateway)
    .WithEnvironment("ApiBaseUrl", "http://gateway/")
    .WithEnvironment("HubBaseUrl", gateway.GetEndpoint("http"))
    .WithExternalHttpEndpoints();

builder.Build().Run();
