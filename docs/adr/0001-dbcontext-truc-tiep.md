# ADR 0001 — Dùng `DbContext` trực tiếp, chưa thêm Repository + Unit of Work

- **Trạng thái:** Chấp nhận (tag `b47-auth-cache`)
- **Bối cảnh buổi học:** 42–47 (lab `dotnet-webapi-lab` tag `b45` đã dạy Repository + UoW)
- **Xem lại ở:** `b48-clean-arch` → đã xem lại: [ADR 0002](0002-clean-architecture-port-hep.md) thay thế một phần (Application dùng port hẹp; bên trong Infrastructure vẫn dùng `DbContext` trực tiếp như ADR này)

## Bối cảnh

Lab webapi `b45` bọc EF Core bằng `IRepository<T>`, `IProductRepository`, `IUnitOfWork.SaveChangesAsync()`. Câu hỏi: CyberCafe có nên làm y hệt ở chặng 42–47 không?

Hiện trạng CyberCafe ở `b47`:

- Chỉ 1 nguồn dữ liệu (SQL Server qua EF Core), 1 project Api.
- Controller/Service dùng `CyberCafeDbContext` trực tiếp: `db.Products.Where(...).Select(ProductMapping.ToDtoExpression)`, `Include(...)`, `EF.Property<int?>(o, "UserId")`, `Database.SqlQuery(...)`.
- Test tích hợp thay SQL Server bằng EF Core InMemory qua `WebApplicationFactory` — không cần mock repository.

## Quyết định

Ở `b47` **không** thêm Repository/UoW. Controller và `AuthService` dùng `DbContext` trực tiếp.

## Lý do

1. **`DbContext` đã là Unit of Work, `DbSet<T>` đã là Repository.** `SaveChangesAsync()` gom mọi thay đổi vào 1 transaction (đặt đơn ghi 4 bảng trong 1 lần — xem `OrdersController.Place`). Bọc thêm `IUnitOfWork.SaveChangesAsync()` chỉ gọi lại đúng hàm đó.
2. **Giữ được sức mạnh của LINQ/IQueryable.** Projection sang DTO, `Include/ThenInclude`, lọc theo shadow property, `AsNoTracking` đều viết ngay tại chỗ dùng. Repository kiểu `GetAll()` trả `IEnumerable` dễ làm mất các tối ưu này (kéo cả bảng về RAM), còn trả `IQueryable` thì "rò" EF ra ngoài — mất ý nghĩa của lớp bọc.
3. **Test không cần repository.** Lý do phổ biến nhất để thêm repository là "mock được trong test". Ở đây test chạy cả pipeline thật với EF InMemory (và kiểm tra migration bằng `HasPendingModelChanges`), tin cậy hơn mock.
4. **Ít code hơn cho 1 chặng đã nặng** (JWT, refresh token, cache, rate limit, middleware). Học viên tập trung vào bảo mật/hiệu năng.

## Hệ quả

- (+) Ít lớp trung gian, đọc code thấy ngay câu SQL sẽ sinh ra.
- (+) Không có "repository mỏng" chỉ chuyển tiếp lời gọi.
- (−) Controller/service phụ thuộc trực tiếp EF Core → đổi sang Dapper/MongoDB phải sửa nhiều chỗ.
- (−) Logic truy vấn lặp lại có thể rải rác (đã giảm bằng helper như `OrdersWithDetails()` và `ProductMapping.ToDtoExpression`).

## Khi nào xem lại

`b48-clean-arch` tách `CyberCafe.Application` (use case) khỏi `CyberCafe.Infrastructure` (EF Core). Khi đó Application **không được** tham chiếu EF Core → mới thật sự cần interface truy cập dữ liệu (repository theo aggregate, hoặc interface `ICyberCafeDbContext` tối giản). Quyết định cụ thể ghi ở ADR mới của b48.

## Đối chiếu với lab

| Lab webapi `b45` | CyberCafe `b47` |
|------------------|-----------------|
| `IRepository<T>`, `Repository<T>` | `DbSet<T>` |
| `IUnitOfWork.SaveChangesAsync()` | `DbContext.SaveChangesAsync()` |
| Service layer gọi repository | Controller mỏng + `AuthService`, `RevenueReportService` dùng `DbContext` |
| Test repository với InMemory | Test tích hợp toàn pipeline với InMemory |
