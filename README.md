# CyberCafe ☕ — Dự án xương sống khóa .NET Full-stack

CyberCafe là ứng dụng **đặt món quán cà phê**: khách xem thực đơn, chọn size, thêm vào giỏ, đặt hàng; nhân viên pha chế (barista) nhận đơn mới; quản trị viên quản lý thực đơn.

Đây là **một codebase duy nhất** được phát triển dần qua các buổi 23 → 55 của khóa .NET Full-stack (giảng viên TienNHM). Mỗi mốc quan trọng được đánh dấu bằng một **git tag** — bạn nghỉ buổi nào chỉ cần checkout đúng tag là có code của buổi đó.

> Domain (Product/Coffee/Tea/Cake, Order/OrderItem, Discount, Payment, Customer/Employee) được port từ bài OOP `day-04/CoffeeShopManagement` — các bạn sẽ gặp lại đúng những class đã viết ở phần OOP.

---

## Dùng tag để theo kịp buổi học

```bash
# 1. Clone repo (1 lần)
git clone <url-repo> cybercafe
cd cybercafe

# 2. Xem danh sách mốc + mô tả
git tag -l -n1

# 3. Xem code đúng như cuối buổi 28 (chế độ "detached HEAD" — chỉ để xem/chạy)
git checkout b28-cart-state

# 4. Muốn code tiếp từ mốc đó → tạo nhánh riêng của bạn
git switch -c my-work b28-cart-state

# 5. So sánh 2 mốc: buổi này thêm/sửa gì?
git diff b23-blazor-start b28-cart-state --stat

# 6. Quay về bản mới nhất
git switch main
```

> ⚠️ Đang có thay đổi chưa commit thì `git checkout <tag>` sẽ báo lỗi. Hãy `git stash` hoặc commit vào nhánh riêng trước.

---

## Cách chạy

Yêu cầu: **.NET SDK 10** (repo pin `10.0.100` trong `global.json`, cho phép roll-forward lên bản 10.0.x mới hơn). Từ tag `b40-api-efcore` cần thêm **Docker Desktop** (SQL Server, từ `b47-auth-cache` thêm Redis, chạy trong container).

### Chỉ build + test (không cần Docker)

```bash
dotnet --version              # 10.0.x
dotnet build                  # build cả solution CyberCafe.slnx
dotnet test                   # unit test + test tích hợp Api (EF Core InMemory, không cần SQL Server/Redis)
```

### Chạy đầy đủ (từ `b40-api-efcore`)

```bash
# 1. SQL Server 2022 + Redis 7 trong Docker (cổng 1434 / 6380, mật khẩu giả trong .env.example)
cp .env.example .env          # tùy chọn — không có .env thì dùng giá trị mặc định
docker compose up -d
docker compose ps             # đợi STATUS = healthy

# 2. Tạo database CyberCafeDb + bảng + dữ liệu mẫu + stored procedure + bảng Users
dotnet tool restore           # cài dotnet-ef theo dotnet-tools.json
# từ b48: migration nằm ở Infrastructure (-p), cấu hình đọc từ Api (-s)
dotnet ef database update -p src/CyberCafe.Infrastructure -s src/CyberCafe.Api
# (tag b40/b47: dotnet ef database update -p src/CyberCafe.Api)

# 3. Chạy 2 project ở 2 terminal
dotnet run --project src/CyberCafe.Api    # http://localhost:5180  (Scalar: /scalar/v1)
dotnet run --project src/CyberCafe.Web    # http://localhost:5170
```

| Thành phần | Địa chỉ | Ghi chú |
|------------|---------|---------|
| Web (Blazor Server) | http://localhost:5170 | `ApiBaseUrl` trong `src/CyberCafe.Web/appsettings.json` |
| Api | http://localhost:5180 | `/scalar/v1` thử API, `/openapi/v1.json`, hub `/hubs/orders` |
| SQL Server | `localhost,1434` | user `sa`, mật khẩu **giả** `Fake_Passw0rd_ChangeMe` (chỉ dev) |
| Redis | `localhost:6380` | cache thực đơn (`Redis:Enabled` trong `appsettings.Development.json`) |

### Tài khoản dev (từ `b47-auth-cache`)

Chỉ được tạo khi chạy môi trường **Development** (`Seed:DevAccounts = true`), mật khẩu là giá trị **giả** `Dev@12345` trong `appsettings.Development.json`. Không dùng cho môi trường thật.

| Email | Vai trò | Được làm |
|-------|---------|----------|
| `admin@cybercafe.local` | Admin | Quản lý thực đơn, báo cáo doanh thu, mọi việc của barista |
| `barista@cybercafe.local` | Barista | Màn hình quầy, đổi trạng thái đơn |
| `customer@cybercafe.local` | Customer | Đặt hàng, xem/hủy đơn của mình (hoặc tự đăng ký tài khoản mới) |

Khóa JWT trong `appsettings.Development.json` cũng là giá trị **giả**; môi trường thật đặt `Jwt__Key` bằng biến môi trường / user-secrets.

Dọn dẹp: `docker compose down` (giữ dữ liệu) hoặc `docker compose down -v` (xóa luôn database).

Gợi ý demo (từ tag `b47-auth-cache`): tab 1 đăng nhập `barista@…` → **Quầy barista**; tab 2 đăng nhập `customer@…` → **Thực đơn** → thêm món → **Đặt hàng** → đơn hiện ngay ở tab barista → bấm *Bắt đầu pha* → trang xác nhận đơn của khách tự đổi trạng thái. Tab 3 đăng nhập `admin@…` → **Quản lý thực đơn** (xóa món đã bán → 409). Khách gõ URL `/barista` → "Không có quyền truy cập".

---

## Cấu trúc thư mục (tại `b53-ddd-cqrs`)

Sơ đồ tầng + luật phụ thuộc: [docs/architecture.md](docs/architecture.md).

```
cybercafe/
├── CyberCafe.slnx               # solution (định dạng XML mới)
├── global.json                  # pin .NET SDK 10
├── Directory.Build.props        # Nullable, ImplicitUsings cho mọi project
├── Directory.Packages.props     # version NuGet tập trung (Central Package Management)
├── dotnet-tools.json            # dotnet-ef (dotnet tool restore)
├── docker-compose.yml           # SQL Server 2022 + Redis 7 (+ .env.example)
├── .github/workflows/ci.yml     # CI: restore → build → test
├── src/
│   ├── CyberCafe.Domain/        # C# thuần, không phụ thuộc web/EF
│   │   ├── Common/              # (b49–50) AggregateRoot, DomainException, IDomainEvent, Money, PhoneNumber
│   │   ├── Products/            # Product → Drink → Coffee/Tea; Cake; DrinkSize
│   │   ├── Orders/              # aggregate Order, OrderItem, OrderCode, Events/, Cart, OrderStatus, OrderStatusFlow
│   │   ├── Discounts/           # Discount → Member/Voucher; DiscountCatalog
│   │   ├── Payments/            # Payment → Cash/Card/Momo; PaymentFactory
│   │   └── People/              # Person → Customer / Employee
│   ├── CyberCafe.Contracts/     # DTO/request dùng chung Api ↔ Web, tên sự kiện SignalR
│   ├── CyberCafe.Application/   # (b48) use case + port — KHÔNG biết EF Core / ASP.NET Core
│   │   ├── Common/              # CurrentUser, exception nghiệp vụ, IUnitOfWork; (b51–52) Messaging/ (ISender…), Behaviors/
│   │   ├── Products/            # MenuService (ghi), Queries/ (GetMenu…), IProductRepository, ProductMapping
│   │   ├── Orders/              # Commands/, Queries/ (+ IOrderReadStore), EventHandlers/, IOrderRepository, IOrderNotifier
│   │   ├── Auth/ Caching/ Reports/  # IAuthService, IMenuCache, IRevenueReportService
│   │   └── DependencyInjection.cs   # AddApplication()
│   ├── CyberCafe.Infrastructure/ # (b48) cài đặt các port
│   │   ├── Persistence/         # CyberCafeDbContext (+IUnitOfWork, phát domain event), Configurations/, Migrations/,
│   │   │                        #   Repositories/ (ghi), ReadModels/ (đọc — b53), MenuSeed
│   │   ├── Identity/            # User, RefreshToken, JWT TokenService, BCrypt, AuthService, DevAccountSeeder
│   │   ├── Caching/             # MenuCache (Redis / IDistributedCache)
│   │   ├── Realtime/            # SignalROrderNotifier<THub>, IOrderClient
│   │   ├── Reports/             # Doanh thu theo ngày (stored procedure / LINQ)
│   │   └── DependencyInjection.cs   # AddInfrastructure(configuration), AddOrderNotifier<THub>()
│   ├── CyberCafe.Api/           # ASP.NET Core Web API — controller mỏng + composition root
│   │   ├── Auth/                # Policies, ToCurrentUser(), Bearer cho OpenAPI
│   │   ├── Controllers/         # Auth, Products, Orders (gửi command/query qua ISender), Reports
│   │   ├── Errors/              # DomainExceptionHandler (exception → ProblemDetails)
│   │   ├── Filters/             # [InvalidateMenuCache]
│   │   ├── Middleware/          # CorrelationId, RequestLogging
│   │   └── Realtime/            # OrderHub (SignalR)
│   └── CyberCafe.Web/           # Blazor Web App (Interactive Server)
│       ├── Components/Pages/    # Home, Menu, CartPage, Checkout, OrderConfirmation, Barista, Admin/MenuAdmin, Login, Register, MyOrders
│       ├── Components/Shared/   # ProductCard, CartSummary, OrderStatusBadge
│       ├── Models/              # CheckoutModel (form), AddToCartArgs
│       ├── Services/            # MenuApiClient, OrderApiClient, AuthApiClient (typed HttpClient), OrderHubConnectionFactory
│       ├── Services/Auth/       # AuthSession, JwtAuthenticationStateProvider, BearerTokenHandler (DelegatingHandler)
│       └── State/               # CartState (scoped + event OnChange)
├── tests/
│   ├── CyberCafe.Tests/         # xUnit: Domain + Web (typed client với HttpMessageHandler giả)
│   ├── CyberCafe.Application.Tests/ # (b53) handler, pipeline behavior, dispatcher — port giả, không DB
│   ├── CyberCafe.Api.Tests/     # WebApplicationFactory + EF InMemory + SignalR qua TestServer
│   └── CyberCafe.ArchitectureTests/ # (b48) luật phụ thuộc giữa các tầng; (b53) luật DDD/CQRS (Reflection)
├── docs/architecture.md         # sơ đồ tầng (Mermaid), đặt code mới ở đâu
├── docs/sessions/               # kịch bản live-code cho từng chặng
├── docs/adr/                    # quyết định kiến trúc (ADR 0001 DbContext, 0002 Clean Architecture, 0003 DDD + CQRS)
├── ROADMAP.md                   # kế hoạch các tag tiếp theo
└── README.md
```

---

## Bảng mốc

| Tag | Buổi | Nội dung chính | Trạng thái |
|-----|------|----------------|------------|
| `b23-blazor-start` | 23 | Blazor Web App, layout + routing, Razor syntax, `MenuService` + DI | ✅ |
| `b28-cart-state` | 24–31 | Component, `[Parameter]`, `EventCallback`, binding, lifecycle, `CartState`, `EditForm` + validation, domain OOP | ✅ |
| `b40-api-efcore` | 32–41 | `CyberCafe.Api` + typed `HttpClient`, EF Core SQL Server (Docker, TPH, migration, stored procedure), SignalR quầy barista | ✅ |
| `b47-auth-cache` | 42–47 | JWT + BCrypt + refresh token, phân quyền, middleware, filter, Redis cache, rate limiting | ✅ |
| `b48-clean-arch` | 48 | Clean Architecture: Domain / Application / Infrastructure / Api, port + adapter, architecture test | ✅ |
| `b53-ddd-cqrs` | 49–53 | Order aggregate, value object, domain event, CQRS (dispatcher tự viết), pipeline behavior, read model | ✅ |
| `b55-microservice` | 54–55 | .NET Aspire, YARP gateway, Order/Payment/Menu services, message broker | ⏳ |

Chi tiết từng mốc: [ROADMAP.md](ROADMAP.md). Kịch bản giảng: [docs/sessions/](docs/sessions/).

---

## Quy ước

- Định danh trong code bằng **tiếng Anh**; giao diện và chú thích bằng **tiếng Việt có dấu**.
- Mỗi mốc phải `dotnet build` + `dotnet test` xanh trước khi gắn tag.
- Commit message tiếng Anh, tiền tố theo buổi: `b28: ...`.

## License

[MIT](LICENSE) © TienNHM / tiennhm-edu
