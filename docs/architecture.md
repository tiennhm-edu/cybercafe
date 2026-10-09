# Kiến trúc CyberCafe (từ tag `b48-clean-arch`, cập nhật `b53-ddd-cqrs`)

CyberCafe theo **Clean Architecture**: nghiệp vụ ở giữa, hạ tầng ở ngoài, mũi tên phụ thuộc **chỉ hướng vào trong**.
Từ `b53`, luồng đơn hàng theo **DDD** (aggregate `Order`, value object, domain event) + **CQRS** (command/query qua dispatcher tự viết).
Quyết định chi tiết: [ADR 0002](adr/0002-clean-architecture-port-hep.md), [ADR 0003](adr/0003-cqrs-dispatcher-tu-viet.md). Kịch bản giảng: [b48](sessions/b48.md), [b49](sessions/b49.md) … [b53](sessions/b53.md).

## Sơ đồ tầng

```mermaid
flowchart TB
    subgraph Outer["Vòng ngoài — chi tiết kỹ thuật"]
        Web["CyberCafe.Web<br/>Blazor Server (gọi Api qua HTTP + SignalR)"]
        Api["CyberCafe.Api<br/>controller mỏng (ISender) · OrderHub · middleware · filter<br/>Program.cs = composition root"]
        Infra["CyberCafe.Infrastructure<br/>EF Core SQL Server · Migrations · repository · read model<br/>DbContext phát domain event sau commit<br/>Identity (JWT, BCrypt) · MenuCache (Redis)<br/>SignalROrderNotifier&lt;THub&gt; · báo cáo (SP)"]
    end
    subgraph Inner["Vòng trong — nghiệp vụ"]
        App["CyberCafe.Application<br/>ISender + behavior (Logging, Validation, Transaction)<br/>command/query handler · domain event handler · MenuService<br/>port: IProductRepository, IOrderRepository, IOrderReadStore, IUnitOfWork,<br/>IMenuCache, IOrderNotifier, IAuthService, IRevenueReportService"]
        Domain["CyberCafe.Domain<br/>aggregate Order · Money · PhoneNumber · OrderCode · domain event<br/>Product · Discount · Payment · Customer<br/>C# thuần, không gói NuGet"]
    end
    Contracts["CyberCafe.Contracts<br/>DTO dùng chung Api ↔ Web"]

    Api --> App
    Api -. "chỉ để gọi AddInfrastructure()" .-> Infra
    Infra -- "implement port" --> App
    App --> Domain
    App --> Contracts
    Contracts --> Domain
    Web --> Contracts
    Web --> Domain
```

| Project | Chứa | Được tham chiếu | **Không** được tham chiếu |
|---------|------|-----------------|---------------------------|
| `CyberCafe.Domain` | Aggregate `Order` (invariant, domain event), value object (`Money`, `PhoneNumber`, `OrderCode`), entity, luật nghiệp vụ (`OrderStatusFlow`, giá theo size, giảm giá đa hình) | — (chỉ BCL `System.*`) | Mọi thứ khác |
| `CyberCafe.Contracts` | `ProductDto`, `OrderDto`, `PlaceOrderRequest`, `Roles`, tên sự kiện SignalR | Domain (enum) | EF Core, ASP.NET Core |
| `CyberCafe.Application` | Use case: command/query + handler (b53), `MenuService`; dispatcher `ISender`, pipeline behavior, domain event publisher; port (interface), mapping Domain → DTO, exception nghiệp vụ, `CurrentUser` | Domain, Contracts, `Microsoft.Extensions.*.Abstractions`, FluentValidation | EF Core, ASP.NET Core, Redis, BCrypt, Infrastructure, Api |
| `CyberCafe.Infrastructure` | `CyberCafeDbContext` (+ `IUnitOfWork`, phát domain event sau commit), Configurations (value converter `Money`), **Migrations**, repository (ghi), `OrderReadStore` (đọc), Identity, cache, SignalR adapter, báo cáo | Application (và Domain/Contracts qua đó), EF Core, BCrypt, Redis, ASP.NET Core shared framework | Api |
| `CyberCafe.Api` | Controller, `OrderHub`, middleware, filter, policy, exception handler, `Program.cs` | Application, Infrastructure (composition root) | — |
| `CyberCafe.Web` | Blazor Server UI | Contracts, Domain | Api, Application, Infrastructure (chỉ nói chuyện qua HTTP) |

Các luật trên được **kiểm tra tự động** trong `tests/CyberCafe.ArchitectureTests` (Reflection, không thêm gói): `LayerDependencyTests` (ai tham chiếu ai) và `DddCqrsRulesTests` (aggregate không setter public, VO/event/command bất biến, mỗi request đúng 1 handler).

## Một request đi qua các tầng: `POST /api/orders` (b48)

```mermaid
sequenceDiagram
    participant C as OrdersController (Api)
    participant S as OrderService (Application)
    participant PR as IProductRepository
    participant OR as IOrderRepository
    participant U as IUnitOfWork
    participant N as IOrderNotifier
    participant D as Cart / Order (Domain)
    C->>S: PlaceAsync(request, userId từ token)
    S->>PR: GetByIdsAsync(ids)  → ProductRepository (EF, WHERE Id IN ...)
    S->>D: cart.AddItem(...) · ToOrder(...) · Checkout(payment)
    S->>OR: Add(order, ownerUserId)  → shadow property UserId
    S->>U: SaveChangesAsync()  → CyberCafeDbContext (1 transaction)
    S->>N: OrderPlacedAsync(dto)  → SignalROrderNotifier<OrderHub>
    S-->>C: OrderDto
    C-->>C: 201 Created + Location
```

Lỗi đi ngược lên bằng exception, **chỉ** `DomainExceptionHandler` (Api) biết mã HTTP:

| Ném ở | Exception | HTTP |
|-------|-----------|------|
| Application | `ValidationException` (món không tồn tại, mã giảm giá sai, đổi loại món) | 400 ValidationProblem |
| Application | `NotFoundException` (không có / không phải của bạn) | 404 |
| Application | `ConflictException` (món đã bán, khách hủy đơn đang pha, email trùng) | 409 |
| Infrastructure (Identity) | `AuthenticationFailedException` | 401 |
| Domain | `ArgumentException` / `InvalidOperationException` | 400 / 409 |

## Từ `b53`: `POST /api/orders` qua CQRS + domain event

```mermaid
sequenceDiagram
    participant C as OrdersController
    participant S as ISender (Sender)
    participant L as LoggingBehavior
    participant V as ValidationBehavior
    participant T as TransactionBehavior
    participant H as PlaceOrderCommandHandler
    participant O as Order (aggregate)
    participant DB as CyberCafeDbContext
    participant E as NotifyBaristasOnOrderPlaced
    C->>S: Send(PlaceOrderCommand.From(request, userId))
    S->>L: →
    L->>V: next
    V->>V: PlaceOrderCommandValidator (FluentValidation) — sai → 400
    V->>T: next
    T->>DB: BeginTransaction (SQL Server; InMemory bỏ qua)
    T->>H: next
    H->>O: Order.Create · AddItem · ApplyDiscount · Pay → Raise(OrderPaid, OrderPlaced)
    H->>DB: Add(order) · SaveChangesAsync → giữ event (đang trong transaction)
    T->>DB: Commit → phát event đang chờ
    DB->>E: OrderPlaced → IOrderNotifier → SignalR group "baristas"
    H-->>C: OrderDto → 201 Created
```

| | GHI (command) | ĐỌC (query) |
|-|---------------|-------------|
| Gửi từ | `OrdersController` (POST/PUT) | `OrdersController` (GET), `ProductsController` (GET), `OrderHub.WatchOrder` |
| Use case | `PlaceOrderCommand`, `ChangeOrderStatusCommand`, `CancelOrderCommand` | `GetOrderByIdQuery`, `GetMyOrdersQuery`, `GetBaristaBoardQuery`, `GetMenuQuery`, `GetProductByIdQuery` |
| Port | `IOrderRepository` (nguyên aggregate, tracked) + `IUnitOfWork` | `IOrderReadStore` (projection → DTO, `AsNoTracking`), `IProductRepository` + `IMenuCache` |
| Luật | Aggregate `Order` (invariant) → `DomainException` 409 | IDOR trong `WHERE` |
| Transaction | Có (`TransactionBehavior`) | Không |

## Đặt code mới ở đâu?

| Bạn cần… | Đặt ở | Ví dụ |
|----------|-------|-------|
| Luật nghiệp vụ không phụ thuộc gì | Domain (method của aggregate) | "Ready rồi thì không hủy được" → `Order.Cancel` |
| Giá trị có luật riêng, so theo giá trị | Domain value object | `Money`, `PhoneNumber` |
| "Sau khi X xảy ra thì làm Y" | Domain event + handler ở Application | `OrderPlaced` → `NotifyBaristasOnOrderPlaced` |
| 1 use case ghi / đọc | Command / query + handler (+ validator) ở Application | `PlaceOrder.cs`, `OrderQueries.cs` |
| Việc cắt ngang mọi use case | Pipeline behavior | `LoggingBehavior` |
| Truy cập dữ liệu / dịch vụ ngoài | Interface ở Application **+** cài đặt ở Infrastructure | `IOrderRepository` ← `OrderRepository` |
| Chi tiết HTTP (route, policy, header, mã trạng thái) | Api | `ProductsController`, `[InvalidateMenuCache]` |
| DTO dùng chung với Web | Contracts | `OrderDto` |

## Lệnh EF Core (từ `b48`)

DbContext và migration nằm ở **Infrastructure**, cấu hình (connection string) đọc từ **Api** → luôn truyền cả 2:

```bash
dotnet ef database update -p src/CyberCafe.Infrastructure -s src/CyberCafe.Api
dotnet ef migrations add <Ten> -p src/CyberCafe.Infrastructure -s src/CyberCafe.Api -o Persistence/Migrations
```

Id các migration cũ giữ nguyên khi chuyển project → database tạo từ `b40`/`b47` dùng tiếp, không cần migration mới.
