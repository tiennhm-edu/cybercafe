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
| `b32-webapi-crud` | 32–35 | Tách `CyberCafe.Api`, typed HttpClient service, CRUD thực đơn | ⏳ planned |
| `b34-signalr` | 33–34 | SignalR Hub: đơn mới realtime cho quầy barista, phân quyền | ⏳ planned |
| `b41-efcore` | 36–41 | SQL Server Docker, EF Core Code First, migrations, LINQ, stored procedure | ⏳ planned |
| `b47-auth-filters` | 42–47 | JWT + BCrypt, middleware, filters, caching Redis, Repository + UoW | ⏳ planned |
| `b48-clean-arch` | 48 | Domain / Application / Infrastructure / Api | ⏳ planned |
| `b53-ddd-cqrs` | 49–53 | Order aggregate, CQRS | ⏳ planned |
| `b55-microservice` | 51–55 | .NET Aspire AppHost, YARP gateway, Order/Payment/Menu services, Kafka hoặc RabbitMQ | ⏳ planned |

> Ghi chú về thứ tự: `b34-signalr` có số buổi nằm trong khoảng 32–35. Thứ tự **commit** dự kiến: `b32-webapi-crud` → `b34-signalr` → `b41-efcore` → ... (b34 xây trên Api của b32).

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

## ⏳ `b32-webapi-crud` — Buổi 32–35

Mục tiêu: dữ liệu menu đi qua HTTP API thay vì nằm trong Web.

- [ ] `dotnet new webapi -n CyberCafe.Api -o src/CyberCafe.Api --use-controllers`; add vào `.slnx`, reference `CyberCafe.Domain`
- [ ] DTO: `ProductDto`, `CreateProductRequest`, `UpdateProductRequest` (record) + mapping Product ↔ DTO (polymorphic: trường `Type` = coffee/tea/cake)
- [ ] `ProductsController`: `GET /api/products`, `GET /api/products/{id}`, `POST`, `PUT`, `DELETE`; trả `201 Created` + `Location`, `404`, `400 ValidationProblem`
- [ ] `IMenuRepository` + `InMemoryMenuRepository` (singleton) — chuẩn bị thay bằng EF ở b41
- [ ] OpenAPI (`AddOpenApi` + Scalar UI) để thử API
- [ ] Web: `MenuApiClient` typed HttpClient (`AddHttpClient<MenuApiClient>(c => c.BaseAddress = ...)`), thay `MenuService` trong trang Menu
- [ ] Trang admin `/admin/menu`: bảng + form thêm/sửa (`EditForm`), nút xóa có confirm
- [ ] `OrdersController` `POST /api/orders` (Checkout gọi API thay vì `OrderStore` trực tiếp)
- [ ] Xử lý lỗi gọi API ở Web (try/catch `HttpRequestException`, hiện thông báo)
- [ ] Test: `WebApplicationFactory<Program>` integration test cho `ProductsController` (GET list, POST → 201, GET id không tồn tại → 404)
- [ ] Chạy song song 2 project: launch profile / hướng dẫn README; CORS nếu cần
- [ ] `docs/sessions/b32.md`

## ⏳ `b34-signalr` — Buổi 33–34

Mục tiêu: quầy barista thấy đơn mới ngay lập tức, cập nhật trạng thái đơn realtime cho khách.

- [ ] `OrderHub : Hub` trong `CyberCafe.Api` (`MapHub<OrderHub>("/hubs/orders")`)
- [ ] Khi `POST /api/orders` thành công → `IHubContext<OrderHub>.Clients.Group("baristas").SendAsync("OrderPlaced", dto)`
- [ ] Trang `/barista` (Web): `HubConnectionBuilder`, danh sách đơn `Pending/Preparing/Ready`, nút chuyển trạng thái
- [ ] `PUT /api/orders/{id}/status` → broadcast `OrderStatusChanged` tới group `order-{id}` → trang `/orders/{id}` của khách tự cập nhật
- [ ] Phân quyền tạm thời: chọn vai trò (Khách / Barista / Admin) + `[Authorize(Roles = "Barista")]` trên hub method (chuẩn bị cho JWT ở b47); ẩn menu nav theo vai trò
- [ ] Xử lý reconnect (`WithAutomaticReconnect`), `IAsyncDisposable` dispose connection
- [ ] Âm báo / badge số đơn mới trên NavMenu
- [ ] Test: unit test service đổi trạng thái (state machine `OrderStatus` hợp lệ: Pending → Preparing → Ready → Completed)
- [ ] `docs/sessions/b34.md`

## ⏳ `b41-efcore` — Buổi 36–41

Mục tiêu: dữ liệu bền vững với SQL Server.

- [ ] `docker-compose.yml`: `mcr.microsoft.com/mssql/server:2022-latest`, volume, healthcheck; hướng dẫn README
- [ ] `CyberCafeDbContext` (`DbSet<Product>`, `Order`, `OrderItem`, `Customer`, `Employee`, `Voucher`)
- [ ] Mapping kế thừa: TPH cho `Product` (discriminator `ProductType`) — so sánh TPT/TPC trong buổi học
- [ ] Fluent API: `decimal(18,2)`, độ dài chuỗi, owned/value conversion cho `DrinkSize`, `OrderStatus`
- [ ] Domain cần constructor rỗng `private` cho EF; backing field cho collection `Items`
- [ ] `dotnet ef migrations add InitialCreate`, `database update`; seed dữ liệu menu (`HasData`)
- [ ] `EfMenuRepository`, `EfOrderRepository` thay bản in-memory
- [ ] LINQ: lọc/sắp xếp/phân trang menu (`Skip/Take`), `Include` order items, projection sang DTO, `AsNoTracking`
- [ ] Báo cáo doanh thu theo ngày bằng LINQ `GroupBy` **và** stored procedure `sp_DailyRevenue` (tạo trong migration, gọi `FromSql`)
- [ ] Transaction khi đặt đơn; xử lý concurrency (`rowversion`) khi admin sửa giá
- [ ] Test: EF Core SQLite in-memory hoặc Testcontainers cho repository
- [ ] `docs/sessions/b36.md` … `b41.md` (gộp theo chủ đề)

## ⏳ `b47-auth-filters` — Buổi 42–47

Mục tiêu: bảo mật + hiệu năng + tổ chức truy cập dữ liệu.

- [ ] Bảng `Users` (Email, PasswordHash, Role); đăng ký/đăng nhập, hash bằng **BCrypt.Net-Next**
- [ ] JWT: `AddAuthentication().AddJwtBearer`, phát access token (+ refresh token tùy thời lượng), claims role
- [ ] Policy: `Admin` quản lý menu, `Barista` xử lý đơn, `Customer` đặt đơn & xem đơn của mình
- [ ] Web: lưu token, `DelegatingHandler` gắn `Authorization: Bearer`, `AuthenticationStateProvider` tùy biến, `<AuthorizeView>`
- [ ] Middleware tự viết: request logging + correlation id; global exception handler → `ProblemDetails`
- [ ] Filters: `ValidationFilter` (action filter), `ApiKey`/audit filter, exception filter — so sánh với middleware
- [ ] Caching: `IMemoryCache` → `IDistributedCache` Redis (Docker) cho `GET /api/products`; invalidation khi CRUD; thử `HybridCache`
- [ ] Repository + Unit of Work (`IUnitOfWork.SaveChangesAsync`) — thảo luận ưu/nhược khi đã có DbContext
- [ ] Rate limiting cho endpoint đăng nhập
- [ ] Test: integration test 401/403 theo vai trò; unit test password hasher, token service
- [ ] `docs/sessions/b42.md` … `b47.md`

## ⏳ `b48-clean-arch` — Buổi 48

Mục tiêu: tái cấu trúc theo Clean Architecture, không đổi hành vi.

- [ ] Tạo `CyberCafe.Application` (use case / service interface, DTO, validation), `CyberCafe.Infrastructure` (EF, Redis, JWT, BCrypt), giữ `CyberCafe.Domain` thuần
- [ ] `CyberCafe.Api` chỉ còn controller mỏng + composition root (`AddApplication()`, `AddInfrastructure()`)
- [ ] Chiều phụ thuộc: Api → Application → Domain; Infrastructure → Application (implement interface)
- [ ] Architecture test (NetArchTest/ArchUnitNET): Domain không reference Infrastructure/EF
- [ ] Sơ đồ layer trong `docs/architecture.md`
- [ ] Toàn bộ test cũ vẫn xanh (chứng minh refactor an toàn)
- [ ] `docs/sessions/b48.md`

## ⏳ `b53-ddd-cqrs` — Buổi 49–53

Mục tiêu: mô hình hóa nghiệp vụ đơn hàng chặt chẽ + tách đọc/ghi.

- [ ] `Order` thành **aggregate root**: chỉ thay đổi qua method (`AddItem`, `ApplyDiscount`, `Pay`, `StartPreparing`, `MarkReady`, `Complete`, `Cancel`), bảo vệ invariant (không sửa đơn đã thanh toán...)
- [ ] Value objects: `Money`, `PhoneNumber`, `OrderId`; `DrinkSize` + giá thành value object
- [ ] Domain events: `OrderPlaced`, `OrderPaid`, `OrderReady` → handler gửi SignalR / cộng điểm
- [ ] CQRS với MediatR (hoặc tự viết dispatcher): `PlaceOrderCommand`, `ChangeOrderStatusCommand`, `GetMenuQuery`, `GetOrderByIdQuery`
- [ ] Pipeline behavior: validation (FluentValidation), logging, transaction
- [ ] Read model tối ưu cho màn hình barista (projection / Dapper)
- [ ] Test: unit test invariant aggregate, handler test
- [ ] `docs/sessions/b49.md` … `b53.md`

## ⏳ `b55-microservice` — Buổi 51–55

Mục tiêu: tách hệ thống thành các service độc lập, điều phối bằng .NET Aspire.

- [ ] `CyberCafe.AppHost` (.NET Aspire) + `CyberCafe.ServiceDefaults` (OpenTelemetry, health check, service discovery)
- [ ] Tách service: `Menu.Api`, `Order.Api`, `Payment.Api` — mỗi service 1 database riêng
- [ ] API Gateway **YARP** (`Gateway`): route `/menu/*`, `/orders/*`, `/payments/*`, gắn auth JWT tập trung
- [ ] Message broker: **RabbitMQ** (MassTransit) *hoặc* **Kafka** — chọn 1, ghi lý do trong ADR
- [ ] Luồng sự kiện: `OrderPlaced` → Payment xử lý → `PaymentCompleted` → Order cập nhật → barista nhận qua SignalR
- [ ] Outbox pattern để không mất message; idempotent consumer
- [ ] Resilience: `AddStandardResilienceHandler` (retry, circuit breaker, timeout)
- [ ] Aspire dashboard: trace xuyên service; Redis + SQL Server + broker khai báo trong AppHost
- [ ] Docker compose / `azd` để deploy demo (tùy chọn)
- [ ] `docs/sessions/b51.md` … `b55.md`
