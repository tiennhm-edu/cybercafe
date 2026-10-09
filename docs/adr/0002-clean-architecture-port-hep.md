# ADR 0002 — Clean Architecture: Application dùng "port hẹp" theo use case, EF Core lùi về Infrastructure

- **Trạng thái:** Chấp nhận (tag `b48-clean-arch`)
- **Bối cảnh buổi học:** 48 (Clean Architecture)
- **Quan hệ:** làm rõ và **thay thế một phần** [ADR 0001](0001-dbcontext-truc-tiep.md) — ADR 0001 vẫn đúng *bên trong* Infrastructure

## Bối cảnh

Đến `b47`, `CyberCafe.Api` làm mọi thứ: controller viết LINQ trên `CyberCafeDbContext`, đọc shadow property `UserId`, gọi BCrypt, phát JWT, đọc/ghi Redis. ADR 0001 chọn **không** thêm Repository/UoW vì lúc đó chỉ có 1 project — bọc `DbContext` thêm 1 lớp chỉ là chuyển tiếp lời gọi.

Buổi 48 tách solution theo Clean Architecture:

```
Api ──► Application ──► Domain
 │           ▲
 └──► Infrastructure (EF Core, Redis, JWT, BCrypt, SignalR adapter)
```

Luật mới: **Application không được tham chiếu EF Core / ASP.NET Core / Infrastructure** (có architecture test kiểm tra). Use case nằm ở Application nên **không gọi được** `db.Orders.Include(...)` nữa → lần đầu tiên có lý do thật để có interface truy cập dữ liệu. Đây chính là điều kiện "xem lại" mà ADR 0001 đã ghi.

## Các phương án

| # | Phương án | Ưu | Nhược |
|---|-----------|----|-------|
| A | `ICyberCafeDbContext` có `DbSet<T>` đặt ở Application (kiểu template phổ biến trên GitHub) | Ít code, giữ nguyên LINQ | `DbSet<T>`, `Include`, `ToListAsync` là kiểu của EF → Application **phải** tham chiếu gói EF Core → vi phạm chính luật của buổi này |
| B | `IRepository<T>` + `IUnitOfWork` chung chung (như lab webapi `b45`) | Quen thuộc | Trả `IEnumerable` thì mất projection/phân trang ở SQL; trả `IQueryable` thì "rò" EF ra ngoài (ADR 0001, lý do 2) |
| C | **Port hẹp theo nhu cầu use case**: `IProductRepository`, `IOrderRepository`, `IUnitOfWork`, `IMenuCache`, `IOrderNotifier`, `IAuthService`, `IRevenueReportService` | Application sạch thật sự; mỗi method đặt tên theo việc cần (`GetByIdsAsync`, `IsSoldAsync`, `GetPageByStatusAsync`); câu SQL vẫn tối ưu vì nằm trọn ở Infrastructure | Nhiều interface hơn; thêm use case mới có thể phải thêm method vào port |

## Quyết định

Chọn **C**.

1. **Persistence** (`DbContext`, `Configurations/`, `Migrations/`, `MenuSeed`) chuyển sang `CyberCafe.Infrastructure/Persistence`. `CyberCafeDbContext` implement luôn `IUnitOfWork` (chữ ký `SaveChangesAsync(CancellationToken)` có sẵn → không thêm dòng code nào).
2. **Repository hẹp** ở `Infrastructure/Persistence/Repositories`: đọc trả DTO đã projection (`ProductMapping.ToDtoExpression` — chỉ là `System.Linq.Expressions`, không phải EF), ghi trả entity Domain đang được theo dõi.
3. **Use case service** (`MenuService`, `OrderService`) ở Application, là class cụ thể (không cần interface: controller là nơi duy nhất gọi, test tích hợp đi qua HTTP).
4. **Identity** (User, RefreshToken, BCrypt, JWT, `AuthService`, seed dev) **cả khối** sang `Infrastructure/Identity` sau `IAuthService`. `AuthService` vẫn dùng `DbContext` trực tiếp — nó đã ở cùng tầng với EF Core, không có gì phải "giấu" (ADR 0001 tiếp tục áp dụng ở đây). Tương tự `RevenueReportService` (stored procedure).
5. **Lỗi nghiệp vụ** = exception có nghĩa ở Application (`NotFoundException`, `ValidationException`, `ConflictException`, `AuthenticationFailedException`); `DomainExceptionHandler` (Api) là nơi duy nhất dịch sang mã HTTP. Use case không trả `IActionResult`.
6. **"Ai đang gọi"**: Api đổi `ClaimsPrincipal` thành `record CurrentUser(int? UserId, bool IsStaff)` rồi truyền xuống — Application không biết `ClaimsPrincipal`. Luật IDOR dùng chung cho controller và hub (`OrderService.CanWatchAsync`).
7. **SignalR**: `IOrderNotifier` ở Application; adapter `SignalROrderNotifier<THub>` ở Infrastructure là **generic theo hub** vì `OrderHub` (cửa vào, giống controller) ở Api mà Infrastructure không được tham chiếu Api. `Program.cs` chọn hub: `AddOrderNotifier<OrderHub>()`.
8. **Migration giữ nguyên Id** (`20261009114128_InitialCreate`, `..._AddDailyRevenueProcedure`, `..._AddUsersAndRefreshTokens`): chỉ đổi namespace. `__EFMigrationsHistory` lưu Id, không lưu namespace → database đang chạy `b47` **không cần migration mới**; `dotnet ef migrations add Probe` sau khi chuyển cho ra `Up/Down` rỗng (đã xóa). Lệnh mới: `dotnet ef ... -p src/CyberCafe.Infrastructure -s src/CyberCafe.Api`.
9. **Architecture test** bằng Reflection (`Assembly.GetReferencedAssemblies()`), **không thêm gói** NetArchTest/ArchUnitNET: đủ cho 5 luật cần kiểm, không phụ thuộc gói bên thứ ba còn cập nhật cho .NET 10 hay không.

## Hệ quả

- (+) Application/Domain test được không cần database; đổi Redis → cache khác, BCrypt → Argon2, SQL Server → PostgreSQL chỉ sửa Infrastructure.
- (+) Controller còn "mỏng": route, policy, mã HTTP, header — không còn LINQ/EF (architecture test kiểm tra constructor controller không nhận kiểu của Infrastructure).
- (+) Toàn bộ 137 test cũ xanh, chỉ sửa `using` → bằng chứng refactor **không đổi hành vi**.
- (−) Nhiều file hơn; 1 tính năng chạm 3–4 project. Đây là cái giá có chủ đích — đáng khi dự án lớn dần (b53 DDD/CQRS, b55 tách service).
- (−) Port hẹp có thể "phình" khi thêm use case → b53 tách đọc/ghi (CQRS) để port đọc không lẫn với repository ghi.

## Đối chiếu

| `b47` (ADR 0001) | `b48` (ADR này) |
|------------------|-----------------|
| Controller → `CyberCafeDbContext` | Controller → `MenuService`/`OrderService` → `IProductRepository`/`IOrderRepository` → (Infrastructure) `CyberCafeDbContext` |
| `db.SaveChangesAsync()` trong controller | `IUnitOfWork.SaveChangesAsync()` trong use case |
| `MenuCache` (class) inject thẳng | `IMenuCache` (Application) ← `MenuCache` (Infrastructure) |
| `IOrderNotifier` + `SignalROrderNotifier` cùng file ở Api | Interface ở Application, adapter generic ở Infrastructure |
| `AuthService` ở Api | `IAuthService` (Application) ← `AuthService` (Infrastructure, vẫn dùng `DbContext` trực tiếp) |
