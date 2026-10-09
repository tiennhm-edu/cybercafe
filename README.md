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

Yêu cầu: **.NET SDK 10** (repo pin `10.0.100` trong `global.json`, cho phép roll-forward lên bản 10.0.x mới hơn). Từ tag `b40-api-efcore` cần thêm **Docker Desktop** (SQL Server chạy trong container).

### Chỉ build + test (không cần Docker)

```bash
dotnet --version              # 10.0.x
dotnet build                  # build cả solution CyberCafe.slnx
dotnet test                   # unit test + test tích hợp Api (EF Core InMemory, không cần SQL Server)
```

### Chạy đầy đủ (từ `b40-api-efcore`)

```bash
# 1. SQL Server 2022 trong Docker (cổng 1434, mật khẩu giả trong .env.example)
cp .env.example .env          # tùy chọn — không có .env thì dùng giá trị mặc định
docker compose up -d
docker compose ps             # đợi STATUS = healthy

# 2. Tạo database CyberCafeDb + bảng + dữ liệu mẫu + stored procedure
dotnet tool restore           # cài dotnet-ef theo dotnet-tools.json
dotnet ef database update -p src/CyberCafe.Api

# 3. Chạy 2 project ở 2 terminal
dotnet run --project src/CyberCafe.Api    # http://localhost:5180  (Scalar: /scalar/v1)
dotnet run --project src/CyberCafe.Web    # http://localhost:5170
```

| Thành phần | Địa chỉ | Ghi chú |
|------------|---------|---------|
| Web (Blazor Server) | http://localhost:5170 | `ApiBaseUrl` trong `src/CyberCafe.Web/appsettings.json` |
| Api | http://localhost:5180 | `/scalar/v1` thử API, `/openapi/v1.json`, hub `/hubs/orders` |
| SQL Server | `localhost,1434` | user `sa`, mật khẩu **giả** `Fake_Passw0rd_ChangeMe` (chỉ dev) |

Dọn dẹp: `docker compose down` (giữ dữ liệu) hoặc `docker compose down -v` (xóa luôn database).

Gợi ý demo (tag `b40-api-efcore`): mở 2 tab — tab 1 **Quầy barista**, tab 2 **Thực đơn** → thêm món → **Đặt hàng** → đơn hiện ngay ở tab barista → bấm *Bắt đầu pha* → trang xác nhận đơn của khách tự đổi trạng thái. **Quản lý thực đơn** để thêm/sửa/xóa món (xóa món đã bán → báo lỗi 409).

---

## Cấu trúc thư mục (tại `b40-api-efcore`)

```
cybercafe/
├── CyberCafe.slnx               # solution (định dạng XML mới)
├── global.json                  # pin .NET SDK 10
├── Directory.Build.props        # Nullable, ImplicitUsings cho mọi project
├── Directory.Packages.props     # version NuGet tập trung (Central Package Management)
├── dotnet-tools.json            # dotnet-ef (dotnet tool restore)
├── docker-compose.yml           # SQL Server 2022 (+ .env.example)
├── .github/workflows/ci.yml     # CI: restore → build → test
├── src/
│   ├── CyberCafe.Domain/        # C# thuần, không phụ thuộc web/EF
│   │   ├── Products/            # Product → Drink → Coffee/Tea; Cake; DrinkSize
│   │   ├── Orders/              # Cart, Order, OrderItem, OrderStatus, OrderStatusFlow
│   │   ├── Discounts/           # Discount → Member/Voucher; DiscountCatalog
│   │   ├── Payments/            # Payment → Cash/Card/Momo; PaymentFactory
│   │   └── People/              # Person → Customer / Employee
│   ├── CyberCafe.Contracts/     # DTO/request dùng chung Api ↔ Web, tên sự kiện SignalR
│   ├── CyberCafe.Api/           # ASP.NET Core Web API (controllers)
│   │   ├── Controllers/         # Products, Orders, Reports
│   │   ├── Data/                # CyberCafeDbContext, Configurations/ (Fluent API), Migrations/, MenuSeed
│   │   ├── Mapping/             # Domain ⇄ DTO (projection)
│   │   ├── Realtime/            # OrderHub (SignalR), IOrderNotifier
│   │   └── Reports/             # Doanh thu theo ngày (stored procedure / LINQ)
│   └── CyberCafe.Web/           # Blazor Web App (Interactive Server)
│       ├── Components/Pages/    # Home, Menu, CartPage, Checkout, OrderConfirmation, Barista, Admin/MenuAdmin
│       ├── Components/Shared/   # ProductCard, CartSummary, OrderStatusBadge
│       ├── Models/              # CheckoutModel (form), AddToCartArgs
│       ├── Services/            # MenuApiClient, OrderApiClient (typed HttpClient), OrderHubConnectionFactory
│       └── State/               # CartState (scoped + event OnChange)
├── tests/
│   ├── CyberCafe.Tests/         # xUnit: Domain + Web (typed client với HttpMessageHandler giả)
│   └── CyberCafe.Api.Tests/     # WebApplicationFactory + EF InMemory + SignalR qua TestServer
├── docs/sessions/               # kịch bản live-code cho từng chặng
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
| `b47-auth-cache` | 42–47 | JWT + BCrypt + refresh token, phân quyền, middleware, filter, Redis cache, rate limiting | ⏳ |
| `b48-clean-arch` | 48 | Clean Architecture: Domain / Application / Infrastructure / Api | ⏳ |
| `b53-ddd-cqrs` | 49–53 | Order aggregate, domain event, CQRS | ⏳ |
| `b55-microservice` | 54–55 | .NET Aspire, YARP gateway, Order/Payment/Menu services, message broker | ⏳ |

Chi tiết từng mốc: [ROADMAP.md](ROADMAP.md). Kịch bản giảng: [docs/sessions/](docs/sessions/).

---

## Quy ước

- Định danh trong code bằng **tiếng Anh**; giao diện và chú thích bằng **tiếng Việt có dấu**.
- Mỗi mốc phải `dotnet build` + `dotnet test` xanh trước khi gắn tag.
- Commit message tiếng Anh, tiền tố theo buổi: `b28: ...`.

## License

[MIT](LICENSE) © TienNHM / tiennhm-edu
