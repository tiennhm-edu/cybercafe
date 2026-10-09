# ROADMAP — Các mốc (tag) của CyberCafe

Mỗi tag = 1 commit trên `main`, đã `dotnet build` + `dotnet test` xanh. Giảng viên hoàn thiện tag **trước** buổi học tương ứng; trong giờ học live-code lại các bước chính theo `docs/sessions/bXX.md`.

> **Demo từng buổi nằm ở lab, không nằm ở đây.** Mỗi khái niệm được dạy trước trong lab (1 commit + 1 tag mỗi buổi):
>
> | Buổi | Lab | Tag |
> |------|-----|-----|
> | 23–34 | `dotnet-blazor-lab` (Blazor Server, binding, component, form, lifecycle, JS interop, state, HttpClient, SignalR, JWT) | `b23` … `b34` |
> | 35–47 | `dotnet-webapi-lab` (CRUD API, SQL Server, EF Core, Docker, middleware, filter, cache, Repository/UoW, JWT, FE Blazor) | `b35` … `b47` |
>
> CyberCafe là **dự án tích hợp**: sau mỗi chặng, lớp áp dụng lại những gì vừa học trong lab vào một sản phẩm hoàn chỉnh, nên các tag ở đây gom nhiều buổi.

| Tag | Buổi | Nội dung | Trạng thái |
|-----|------|----------|------------|
| `b23-blazor-start` | 23 | Blazor Server buổi 1: Blazor Web App, layout, routing, Razor syntax, DI `MenuService` | ✅ done |
| `b28-cart-state` | 24–31 | Binding, component, parameter, EventCallback, form + validation, lifecycle, state giỏ hàng, domain OOP | ✅ done |
| `b40-api-efcore` | 32–41 | `CyberCafe.Api` (controllers) + EF Core SQL Server (Docker) + SignalR quầy barista; Web gọi API bằng typed HttpClient | ✅ done |
| `b47-auth-cache` | 42–47 | JWT + BCrypt + refresh token, phân quyền theo vai trò, middleware, filter, Redis cache, rate limiting | ✅ done |
| `b48-clean-arch` | 48 | Domain / Application / Infrastructure / Api, architecture test, không đổi hành vi | ✅ done |
| `b53-ddd-cqrs` | 49–53 | Order aggregate, value object, domain event, CQRS (dispatcher tự viết), pipeline behavior, read model | ✅ done |
| `b55-microservice` | 54–55 | .NET Aspire AppHost, YARP gateway, tách Payment service, RabbitMQ (MassTransit 8), Outbox, idempotent consumer | ✅ done |

> Thứ tự commit = thứ tự trong bảng. Mỗi tag gom trọn 1 chặng, không chồng buổi: 32–41 → 42–47 → 48 → 49–53 → 54–55.

---

## ✅ `b23-blazor-start` — Buổi 23

- [x] `dotnet new blazor --interactivity Server --all-interactive` → `src/CyberCafe.Web`
- [x] `global.json`, `Directory.Build.props`, `.editorconfig`, `.gitignore`, `CyberCafe.slnx`, CI
- [x] MainLayout + NavMenu (Bootstrap), trang Home `/`, Menu `/menu`, About `/about`, NotFound
- [x] `MenuService` (seed in-memory) đăng ký Singleton; Razor `@foreach`, `@if`, `@code`
- [x] xUnit `MenuServiceTests`

Kịch bản: [docs/sessions/b23.md](docs/sessions/b23.md)

## ✅ `b28-cart-state` — Buổi 24–31

- [x] `src/CyberCafe.Domain`: port hierarchy OOP từ `day-04/CoffeeShopManagement` (Product/Drink/Coffee/Tea/Cake, Cart/Order/OrderItem, Discount, Payment, Person/Customer/Employee)
- [x] `ProductCard` (`[Parameter]`, `EventCallback<AddToCartArgs>`, `@bind` size/số lượng), `CartSummary`
- [x] `CartState` scoped + `event Action OnChange` + `InvokeAsync(StateHasChanged)` + `IDisposable`
- [x] Lifecycle: `OnInitializedAsync` (load menu), `OnAfterRender(firstRender)`, `OnParametersSet`
- [x] `/cart` (sửa số lượng, xóa, mã giảm giá), `/checkout` (`EditForm` + `DataAnnotationsValidator` + `IValidatableObject`), `/orders/{id}`
- [x] `OrderStore` singleton in-memory; unit test giỏ hàng, giảm giá, thanh toán, validate form

Kịch bản: [docs/sessions/b28.md](docs/sessions/b28.md)

---

## ✅ `b40-api-efcore` — Buổi 32–41

Mục tiêu: dữ liệu đi qua HTTP API và nằm bền vững trong SQL Server; quầy barista nhận đơn realtime.

- [x] `src/CyberCafe.Api` (controllers) + `src/CyberCafe.Contracts` (DTO/request dùng chung Api ↔ Web); Central Package Management (`Directory.Packages.props`, cùng version với lab)
- [x] `ProductsController`: GET (lọc/tìm/sắp xếp/phân trang), GET id, POST `201 + Location`, PUT, DELETE (`409` nếu món đã bán); `404` + `ValidationProblem`
- [x] `OrdersController`: POST đặt hàng (server tự tính tiền, tra mã giảm giá), GET id, GET danh sách đơn đang chạy, PUT status theo `OrderStatusFlow` (Pending → Preparing → Ready → Completed, hủy khi chưa Ready) → `409` khi sai luồng
- [x] EF Core SQL Server: `CyberCafeDbContext`, `IEntityTypeConfiguration`, **TPH** cho Product (và Payment, Discount), owned type `Customer`, shadow key/FK, enum → string, `decimal` precision, constructor private cho EF, chốt `UnitPrice` trên `OrderItem`
- [x] Migration `InitialCreate` + seed `HasData`; migration viết tay `AddDailyRevenueProcedure` tạo `dbo.usp_DailyRevenue`, gọi bằng `Database.SqlQuery` (tham số hóa); InMemory dùng nhánh LINQ `GroupBy`
- [x] `AsNoTracking`, projection sang DTO, `Include/ThenInclude` cho đơn hàng
- [x] `docker-compose.yml` SQL Server 2022 (healthcheck, volume, cổng 1434), `.env.example`, `dotnet-tools.json` (dotnet-ef)
- [x] OpenAPI + Scalar (`/scalar/v1`)
- [x] SignalR `OrderHub`: group `baristas` + `order-{id}`; `IOrderNotifier` bọc `IHubContext`
- [x] Web: `MenuApiClient`, `OrderApiClient` (typed HttpClient, `ApiException`), trang `/barista` (HubConnection, `WithAutomaticReconnect`, `IAsyncDisposable`), `/orders/{id}` cập nhật trạng thái trực tiếp, `/admin/menu` CRUD + phân trang
- [x] Cổng cố định: Api `5180`, Web `5170`; `ApiBaseUrl` trong `appsettings.json`
- [x] Test: `WebApplicationFactory` + EF InMemory (CRUD, paging, đặt hàng, luồng trạng thái 400/404/409, báo cáo), hub thật qua TestServer, migration up-to-date; unit test typed client bằng `HttpMessageHandler` giả
- [x] CI dùng `global-json-file`

Kịch bản: [docs/sessions/b40.md](docs/sessions/b40.md)

## ✅ `b47-auth-cache` — Buổi 42–47

Mục tiêu: bảo mật theo vai trò + hiệu năng + vận hành an toàn.

- [x] Bảng `Users` (Email, PasswordHash **BCrypt**, Role: Customer/Barista/Admin) + `RefreshTokens` (chỉ lưu hash); migration `AddUsersAndRefreshTokens`; tài khoản dev chỉ seed khi `Seed:DevAccounts = true`
- [x] `POST /api/auth/register|login|refresh|logout`, `GET /api/auth/me`; JWT access token + refresh token xoay vòng (phát hiện dùng lại → thu hồi cả họ)
- [x] Policy: `ManageMenu` (Admin: menu + báo cáo), `ProcessOrders` (Barista/Admin: đổi trạng thái, group hub), `PlaceOrders` (Customer); chặn IDOR ở REST (`GET`, `cancel`, `/mine`) và hub (`WatchOrder`)
- [x] Web: `/login`, `/register`, `/my-orders`; `AuthSession` + `ProtectedSessionStorage`; `JwtAuthenticationStateProvider`; `BearerTokenHandler` (`DelegatingHandler`, refresh 1 lần khi 401); `AuthorizeRouteView`, `<AuthorizeView>` theo vai trò; hub gửi `access_token`; tắt prerender
- [x] Middleware: `CorrelationIdMiddleware`, `RequestLoggingMiddleware`; `IExceptionHandler` → `ProblemDetails` (có `correlationId`)
- [x] Filter có giá trị thật: `[InvalidateMenuCache]`; bảng đối chiếu với các filter của lab b43 trong `docs/sessions/b47.md`
- [x] Redis (`IDistributedCache`) cho `GET /api/products`, key có version để invalidation, header `X-Cache`, tự lùi về DB khi Redis lỗi; Redis trong `docker-compose.yml`
- [x] ADR [0001](docs/adr/0001-dbcontext-truc-tiep.md): dùng `DbContext` trực tiếp, chưa thêm Repository/UoW
- [x] Rate limiting cho đăng nhập/đăng ký (429 + `Retry-After`)
- [x] Test: 401/403 theo vai trò, IDOR, refresh rotation/reuse, hash mật khẩu, cache invalidation, Redis lỗi, rate limit, handler/AuthSession phía Web

Kịch bản: [docs/sessions/b47.md](docs/sessions/b47.md)

## ✅ `b48-clean-arch` — Buổi 48

Mục tiêu: tái cấu trúc theo Clean Architecture, không đổi hành vi.

- [x] Tạo `CyberCafe.Application` (use case `MenuService`/`OrderService`, port, mapping Domain → DTO, exception nghiệp vụ, `CurrentUser`), `CyberCafe.Infrastructure` (EF Core + Configurations + Migrations, repository, Redis `MenuCache`, JWT, BCrypt, `AuthService`, `SignalROrderNotifier<THub>`, báo cáo SP), giữ `CyberCafe.Domain` thuần
- [x] `CyberCafe.Api` chỉ còn controller mỏng, hub, middleware, filter + composition root (`AddApplication()`, `AddInfrastructure(configuration)`, `AddOrderNotifier<OrderHub>()`)
- [x] Chiều phụ thuộc: Api → Application → Domain; Infrastructure → Application (implement interface)
- [x] Interface truy cập dữ liệu **hẹp theo use case** ở Application (`IProductRepository`, `IOrderRepository`, `IUnitOfWork` do `DbContext` implement) — [ADR 0002](docs/adr/0002-clean-architecture-port-hep.md) thay thế một phần ADR 0001
- [x] Migration chuyển sang `Infrastructure/Persistence/Migrations`, **giữ nguyên Id** → DB `b47` không cần migration mới (`migrations add` thử ra rỗng)
- [x] Architecture test (`tests/CyberCafe.ArchitectureTests`, Reflection — không thêm gói): Domain chỉ dùng BCL; Domain/Contracts/Application không reference EF/ASP.NET Core/Infrastructure; controller không nhận kiểu Infrastructure; port nào cũng có adapter
- [x] Sơ đồ layer trong [`docs/architecture.md`](docs/architecture.md)
- [x] Toàn bộ 137 test cũ vẫn xanh, chỉ sửa `using` (chứng minh refactor an toàn)

Kịch bản: [docs/sessions/b48.md](docs/sessions/b48.md)

## ✅ `b53-ddd-cqrs` — Buổi 49–53

Mục tiêu: mô hình hóa nghiệp vụ đơn hàng chặt chẽ + tách đọc/ghi. Lược đồ database **không đổi** (không migration mới), hợp đồng HTTP giữ nguyên — Web không sửa.

- [x] `Order` thành **aggregate root** (`AggregateRoot`, `DomainException` → 409): tạo bằng `Order.Create`, chỉ thay đổi qua method (`AddItem`, `ApplyDiscount`, `Pay`, `StartPreparing`, `MarkReady`, `Complete`, `Cancel`), bảo vệ invariant (không sửa sau thanh toán, không trả 2 lần, chưa trả chưa pha, khách chỉ hủy khi Pending); `OrderStatusFlow` giữ làm bảng luật dùng chung với Web; chủ đơn thành `Order.OwnerId` (cột cũ `UserId`)
- [x] Value objects: `Money` (value converter EF), `OrderCode` (`CC-0007` 2 chiều), `PhoneNumber` (1 nguồn luật SĐT)
- [x] Domain events: `OrderPlaced`, `OrderPaid`, `OrderStatusChanged`, phát **sau khi lưu / sau commit** (override `SaveChangesAsync`); handler gửi SignalR + log thanh toán (thay `IOrderNotifier` gọi tay)
- [x] CQRS với **dispatcher tự viết** (`ISender`, `ICommand`/`IQuery`, handler; MediatR 13+ là thương mại — [ADR 0003](docs/adr/0003-cqrs-dispatcher-tu-viet.md)): `PlaceOrderCommand`, `ChangeOrderStatusCommand`, `CancelOrderCommand`, `GetMenuQuery`, `GetProductByIdQuery`, `GetOrderByIdQuery`, `GetMyOrdersQuery`, `GetBaristaBoardQuery`
- [x] Pipeline behavior: logging, validation (FluentValidation, Apache-2.0), transaction (chỉ command, event chờ commit)
- [x] Read model cho màn hình barista / chi tiết đơn: projection + `AsNoTracking`, IDOR trong `WHERE` (không thêm Dapper — giữ ít gói)
- [x] Test: invariant aggregate + value object (`CyberCafe.Tests`), handler + pipeline + publisher (`CyberCafe.Application.Tests` mới), read model khớp write model (Api.Tests), luật DDD/CQRS (ArchitectureTests)
- [x] `docs/sessions/b49.md` … `b53.md`

Kịch bản: [b49](docs/sessions/b49.md) · [b50](docs/sessions/b50.md) · [b51](docs/sessions/b51.md) · [b52](docs/sessions/b52.md) · [b53](docs/sessions/b53.md)

## ✅ `b55-microservice` — Buổi 54–55

Mục tiêu: tách hệ thống thành các service độc lập, điều phối bằng .NET Aspire. Chạy: `dotnet run --project src/CyberCafe.AppHost` (cần Docker).

- [x] `CyberCafe.AppHost` (Aspire 13.6, `Aspire.AppHost.Sdk` — không cần workload) + `CyberCafe.ServiceDefaults` (OpenTelemetry, health check `/health` `/alive`, service discovery, resilience)
- [x] Tách service, mỗi service 1 database riêng — **có điều chỉnh**: ~~`Menu.Api`, `Order.Api`, `Payment.Api`~~ → `CyberCafe.Api` giữ vai trò **Order service** (+ Menu + Identity, không viết lại — CyberCafeDb), tách **`CyberCafe.Payment.Api`** (minimal API, database `CyberCafePayments`, migration riêng). Tách Menu là bài tập b54 (2 buổi không đủ cho 3 service + broker + outbox).
- [x] API Gateway **YARP** (`CyberCafe.Gateway`, service discovery `http://api` / `http://payment`): route ~~`/menu/*`, `/orders/*`~~ → `/api/*`, `/hubs/*` (giữ hợp đồng HTTP + SignalR của Web), `/payments/*`; **JWT kiểm tập trung ở gateway** cho `/payments/*` (policy `AdminOnly`), `/api/*` chuyển nguyên token để Api tự kiểm policy chi tiết — so sánh trong `b54.md`
- [x] Message broker: **RabbitMQ** qua **MassTransit 8.5.11** (Apache-2.0; 9.x là thương mại — khóa version), lý do RabbitMQ vs Kafka trong [ADR 0004](docs/adr/0004-rabbitmq-masstransit-v8.md); hợp đồng message ở project riêng `CyberCafe.IntegrationEvents` (chỉ BCL)
- [x] Luồng sự kiện: `OrderPlacedIntegrationEvent` → Payment (luật giả lập: vượt 500.000 đ / thẻ đuôi `0000` bị từ chối) → `PaymentCompleted` → `order.Pay` → domain event `OrderPlaced` → barista + khách nhận qua SignalR; `PaymentFailed` → `order.RejectPayment` → `Cancelled`
- [x] **Outbox** tự viết (bảng `OutboxMessages` cùng transaction với đơn, `OutboxPublisherWorker`, nối trace qua `TraceParent`) + **idempotent consumer** (`ProcessedMessages` ở cả 2 service, `UNIQUE(OrderId)` bên Payment) — migration `AddOutboxAndInbox`, [ADR 0005](docs/adr/0005-outbox-idempotent-consumer.md)
- [x] Resilience: `AddStandardResilienceHandler` (không retry POST), `UseMessageRetry` + queue `_error`, outbox thử lại khi broker tắt
- [x] Aspire dashboard: 1 trace xuyên gateway → api → RabbitMQ → payment → api; SQL Server + Redis + RabbitMQ khai báo trong AppHost. `docker-compose.yml` **giữ lại** cho các tag cũ / chạy Api lẻ (`Payments:Flow` mặc định `InProcess` = hành vi b53)
- [x] Test (không cần Docker): 209 test cũ vẫn xanh (chỉ sửa `PipelineTests`: thêm 2 port giả vào DI, ngưỡng đếm request 8 → 10 vì có 2 command mới); thêm test outbox cùng transaction, dispatcher, idempotent consumer (MassTransit test harness), luật thanh toán, định tuyến gateway, ranh giới service (`ServiceBoundaryTests`)
- [x] `docs/sessions/b54.md`, `b55.md` (kèm tổng kết cả dự án); `docs/architecture.md` có sơ đồ topology

Kịch bản: [b54](docs/sessions/b54.md) · [b55](docs/sessions/b55.md)
