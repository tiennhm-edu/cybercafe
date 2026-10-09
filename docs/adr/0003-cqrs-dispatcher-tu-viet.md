# ADR 0003 — DDD cho luồng đơn hàng + CQRS với dispatcher tự viết (không dùng MediatR)

- **Trạng thái:** Chấp nhận (tag `b53-ddd-cqrs`)
- **Bối cảnh buổi học:** 49–53 (DDD building blocks, value object, domain event, CQRS, pipeline behavior, read model)
- **Quan hệ:** xây tiếp trên [ADR 0002](0002-clean-architecture-port-hep.md)

## Bối cảnh

Sau `b48`, luật nghiệp vụ của đơn hàng nằm rải ở 3 nơi:

- `Order` (Domain): `ChangeStatus` + `OrderStatusFlow` — nhưng `Checkout()` gọi 2 lần cũng được, `order.Items[0].Quantity = 99` sau khi đã thu tiền cũng được.
- `OrderService` (Application): "khách chỉ hủy khi Pending", tự gọi `notifier` sau mỗi `SaveChanges`.
- `OrdersController`/`OrderHub`: chọn method nào của `OrderService`.

`OrderService` có 7 method, inject 4 phụ thuộc — method nào cũng nhận đủ 4 dù chỉ dùng 1–2. Đọc và ghi dùng chung `IOrderRepository` → interface phình dần.

## Quyết định

### 1. `Order` là aggregate root (DDD)

- Lớp cha `AggregateRoot` (giữ domain event), `DomainException : InvalidOperationException` (→ 409).
- Tạo bằng `Order.Create(...)`; đổi bằng `AddItem`, `ApplyDiscount`, `Pay`, `StartPreparing`, `MarkReady`, `Complete`, `Cancel(requestedByCustomer)`; `ChangeStatus(target)` chỉ dịch "trạng thái đích" của nút barista sang đúng method.
- Invariant do aggregate tự giữ: không sửa sau khi thanh toán, không thanh toán đơn rỗng/2 lần, chưa trả tiền chưa pha, đi theo `OrderStatusFlow`, khách chỉ hủy khi `Pending`, không thêm món tạm hết.
- `OrderItem` chỉ Domain sửa được (`internal set`, constructor `internal`).
- Chủ đơn thành `Order.OwnerId` (tham chiếu aggregate khác **bằng Id**), map vào **đúng cột cũ** `Orders.UserId`.

### 2. Value object

| Value object | Dùng ở | Lưu DB |
|--------------|--------|--------|
| `Money` (`readonly record struct`, không âm, + − ×, `IFormattable`) | `OrderItem.UnitPrice`, tổng tiền của `Order` | **Value converter** `Money ⇄ decimal(18,2)` — cột `UnitPrice` không đổi |
| `OrderCode` (`CC-0007`, `From` / `TryParse`) | `Order.Code`, `Order.FormatCode` | Không lưu (tính từ Id) |
| `PhoneNumber` (`Create` / `IsValid`) | `Customer`, `Person.IsValidPhoneNumber`, validator của `PlaceOrderCommand` | Vẫn là chuỗi trong `CustomerPhone` — VO là 1 nguồn luật trong code |

Không đổi 1 cột nào → **không có migration mới**. `dotnet ef migrations add` thử cho `Up/Down` rỗng; snapshot được làm mới để ghi nhận `OwnerId ↔ UserId`.

### 3. Domain event, phát SAU khi lưu

- `Pay()` phát `OrderPaid` + `OrderPlaced` (luật quán: trả tiền xong mới vào hàng chờ); mỗi lần đổi trạng thái phát `OrderStatusChanged`. Event giữ **tham chiếu** tới `Order` vì Id (IDENTITY) chỉ có sau khi lưu.
- `CyberCafeDbContext.SaveChangesAsync` (override) lấy event khỏi các aggregate đang theo dõi → không có transaction ngoài thì phát ngay; đang trong `ExecuteInTransactionAsync` thì **chờ commit** rồi mới phát; rollback thì bỏ.
- Handler ở Application: `NotifyBaristasOnOrderPlaced`, `NotifyOnOrderStatusChanged` (thay các lời gọi `notifier` bằng tay), `LogOrderPaid`. Handler lỗi chỉ log, không làm request thất bại (dữ liệu đã lưu). Cần "chắc chắn không mất" → Outbox (b55).
- Cache: đơn hàng không ảnh hưởng thực đơn nên không handler nào cần xóa cache; ghi thực đơn vẫn dùng filter `[InvalidateMenuCache]` (b47).

### 4. CQRS với dispatcher **tự viết**, không dùng MediatR

| Tiêu chí | MediatR 12.x (Apache-2.0) | MediatR 13+ | Tự viết (`Common/Messaging`) |
|----------|---------------------------|-------------|------------------------------|
| Giấy phép | Mã nguồn mở, nhưng nhánh cũ, không còn cập nhật | **Thương mại** (cần license key cho tổ chức có doanh thu vượt ngưỡng) | Không phụ thuộc |
| Dạy được "bên trong hoạt động thế nào" | Hộp đen | Hộp đen | ~120 dòng đọc hiểu được trong 1 buổi |
| Tính năng cần | Send, pipeline behavior, notification | (như bên trái) | `ISender.Send`, `IPipelineBehavior`, `IDomainEventPublisher` — đủ |

Thành phần:

- `IRequest<T>` → `ICommand<T>` (ghi, có `ICommandBase` để nhận diện) / `IQuery<T>` (đọc); `IRequestHandler<TReq, TRes>` (+ tên gọi `ICommandHandler`/`IQueryHandler`).
- `Sender`: dựng `RequestWrapper<TRequest, TResponse>` bằng `MakeGenericType` **1 lần** cho mỗi kiểu request, cache trong `ConcurrentDictionary`; sau đó gọi bằng generic thường. Behavior được bọc từ trong ra ngoài theo thứ tự đăng ký.
- `AddApplication()` quét assembly đăng ký mọi handler, event handler, validator.
- Pipeline: **Logging → Validation → Transaction → Handler**.
  - Validation: **FluentValidation 12 (Apache-2.0)** — luật hình dạng dữ liệu cho command/query (chạy cả khi request không đi qua `[ApiController]`, vd từ hub/job/test). Không dùng gói `FluentValidation.DependencyInjectionExtensions` (bản trong cache lệch version) — tự quét validator trong `AddApplication()`.
  - Transaction: chỉ bọc **command**; EF InMemory không có transaction → chạy thẳng.

### 5. Đọc và ghi tách nhau

| | GHI (command) | ĐỌC (query) |
|-|---------------|-------------|
| Port | `IOrderRepository` (`GetAsync`, `Add`) — tải **nguyên aggregate**, tracked | `IOrderReadStore` — trả **DTO** |
| Cài đặt | `OrderRepository` (Include đủ cụm) | `OrderReadStore`: `AsNoTracking` + projection `Expression` chỉ SELECT cột màn hình cần; IDOR nằm trong `WHERE` |
| Use case | `PlaceOrder`, `ChangeOrderStatus`, `CancelOrder` | `GetOrderById`, `GetMyOrders`, `GetBaristaBoard`, `GetMenu`, `GetProductById` |

Không dùng Dapper/SQL thô cho read model (giữ ít gói, test InMemory chạy được); `ReadModel_MatchesWriteModel` (test tích hợp) đảm bảo 2 phía cho ra cùng số liệu.

### 6. CQRS có chọn lọc

`AuthService` (Identity) và ghi thực đơn (`MenuService`) **giữ nguyên dạng service**: CRUD đơn giản, không có aggregate/invariant đáng kể. CQRS áp cho luồng có nghiệp vụ thật (đơn hàng) và cho đọc thực đơn (đi qua cache).

## Hệ quả

- (+) Luật đơn hàng ở **1 chỗ** (aggregate), test bằng C# thuần (`OrderAggregateTests`).
- (+) Thêm phản ứng mới (email, tích điểm, hóa đơn) = thêm 1 event handler, không sửa command.
- (+) Controller/hub chỉ phụ thuộc `ISender`; cùng 1 query dùng cho REST và SignalR.
- (+) Hợp đồng HTTP **không đổi** — Web không sửa dòng nào; 137 test cũ vẫn xanh. Khác biệt duy nhất quan sát được: khách hủy đơn đang pha → 409 có `title` "Vi phạm quy tắc nghiệp vụ" (trước là "Không hủy được đơn"); `detail` giữ nguyên.
- (−) Nhiều kiểu hơn (record, handler, validator); người mới cần bản đồ `docs/architecture.md`.
- (−) Read model tự tính lại tổng tiền — phải giữ cùng công thức với aggregate (có test canh).
- (−) Domain event trong cùng tiến trình, best-effort; mất điện ngay sau commit thì có thể mất thông báo → Outbox ở b55.
