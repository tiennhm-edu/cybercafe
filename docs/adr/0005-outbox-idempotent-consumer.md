# ADR 0005 — Outbox tự viết (EF Core) + idempotent consumer (bảng ProcessedMessages)

- **Trạng thái:** Chấp nhận (tag `b55-microservice`)
- **Bối cảnh buổi học:** 55 (messaging tin cậy)
- **Quan hệ:** hoàn tất phần "Khi nào xem lại" của [ADR 0003](0003-cqrs-dispatcher-tu-viet.md) (domain event best-effort → Outbox); dùng broker của [ADR 0004](0004-rabbitmq-masstransit-v8.md)

## Bối cảnh

Đặt hàng ở chế độ microservice phải làm 2 việc ở 2 hệ thống:

1. `INSERT` đơn vào SQL Server (CyberCafeDb).
2. Gửi `OrderPlacedIntegrationEvent` lên RabbitMQ để Payment thu tiền.

Không có transaction chung cho SQL Server + RabbitMQ ("dual write"):

| Thứ tự | Sự cố ở giữa | Hậu quả |
|--------|--------------|---------|
| Lưu đơn → gửi message | Api sập / broker tắt sau khi commit | Đơn kẹt "chờ thanh toán" mãi mãi, Payment không biết |
| Gửi message → lưu đơn | Lưu lỗi (deadlock, vi phạm ràng buộc) | Payment thu tiền 1 đơn **không tồn tại** |

Ngược lại, broker chỉ hứa **at-least-once**: 1 message có thể tới 2 lần (consumer xử lý xong nhưng sập trước khi ack; outbox gửi lại sau khi mất kết nối). Xử lý `PaymentCompleted` 2 lần = cộng điểm 2 lần; `OrderPlaced` 2 lần = thu tiền 2 lần.

## Quyết định

### 1. Transactional Outbox (Order service)

- Bảng `OutboxMessages` (Id = EventId, Type, Payload JSON, OccurredAtUtc, **TraceParent**, ProcessedAtUtc, Attempts, LastError) ở **cùng database** với `Orders` — migration `AddOutboxAndInbox` (chỉ thêm bảng, không đổi bảng cũ).
- Port `IIntegrationEventOutbox.Enqueue(...)` ở Application; adapter `EfIntegrationEventOutbox` chỉ `db.OutboxMessages.Add(...)` vào **chính DbContext của request**.
- `PlaceOrderCommandHandler` (nhánh `PaymentFlow.Messaging`): `Add(order)` → `SaveChanges` (lấy Id IDENTITY) → `Enqueue(OrderPlacedIntegrationEvent)` → `SaveChanges`. Cả 2 lần lưu nằm trong transaction của `TransactionBehavior` (b52) → **đơn và message cùng commit hoặc cùng rollback**. (Đây là ví dụ thật đầu tiên cho câu "handler gọi SaveChanges NHIỀU lần" trong `TransactionBehavior`.)
- `OutboxDispatcher` (1 lượt quét, test gọi thẳng) + `OutboxPublisherWorker` (`BackgroundService`, `PeriodicTimer`, `Outbox:PollingIntervalMs` = 1000): đọc dòng chưa gửi theo `OccurredAtUtc` → publish (MessageId = EventId) → ghi `ProcessedAtUtc`, **lưu sau mỗi message**. Lỗi → `Attempts++`, `LastError`, thử lại lượt sau; ≥ 10 lần thì dừng chờ người xem.
- Dispatcher mở span `outbox publish …` là **con của traceparent** đã lưu → trace trên dashboard nối liền POST → publish → Payment.

Phương án đã cân nhắc:

| Phương án | Vì sao không chọn |
|-----------|-------------------|
| MassTransit EF Core Outbox (`AddEntityFrameworkOutbox` + `UseBusOutbox`) | Đúng và mạnh (có inbox, khóa dòng, dọn dẹp), nhưng là hộp đen; lớp cần **thấy** bảng outbox, thứ tự "publish rồi mới đánh dấu" và lý do at-least-once. Ghi ở "Khi nào xem lại". |
| Phát integration event trong domain event handler (sau commit) | Vẫn là dual write — chính vấn đề cần chữa. |
| Đổi `Orders.Id` sang sequence/HiLo để có Id trước khi lưu (1 lần SaveChanges) | Đổi khóa chính IDENTITY của bảng đang có dữ liệu = migration nặng, rủi ro; transaction 2 lần lưu đã đủ. |
| CDC (Debezium đọc transaction log) | Hạ tầng quá nặng cho 2 buổi. |

### 2. Idempotent consumer (cả 2 service)

- Mỗi service 1 bảng `ProcessedMessages` (khóa chính ghép **MessageId + Consumer**) trong database **của mình**.
- **Order service**: `ConfirmOrderPaymentCommand` / `RejectOrderPaymentCommand` hỏi `IInbox.HasProcessedAsync(EventId)` → trùng thì `Duplicate`, dừng; không thì sửa aggregate + `MarkProcessed` + `SaveChanges` (cùng transaction). Message không còn áp dụng được (khách đã tự hủy, đơn đã trả) → `Ignored`, **vẫn** đánh dấu đã xử lý, chỉ log (không ném lỗi → không retry vô hạn).
- **Payment service**: kiểm tra 2 lớp — `EventId` (inbox) **và** khóa nghiệp vụ `UNIQUE(OrderId)` trên bảng `Payments`. Trùng → **gửi lại kết quả cũ với cùng `ResultEventId`** (đã lưu) thay vì im lặng: nếu lần trước sập giữa "lưu" và "publish", đơn bên kia vẫn nhận được kết quả; nếu lần trước đã tới nơi, inbox bên Order bỏ qua. Nhờ vậy Payment **không cần outbox**.
- Hai bản trùng chạy **song song** đều qua bước kiểm tra → bản sau vi phạm khóa chính/unique → exception → MassTransit retry → lần retry thấy "đã xử lý". **Database là trọng tài cuối cùng**, không phải `if` trong code.
- Message tự mang `EventId` (không dựa vào MessageId của broker) → đổi broker / gửi lại từ outbox vẫn chống trùng được.

### 3. Domain event vẫn giữ nguyên

Domain event (b50) vẫn phát sau commit, trong 1 tiến trình (SignalR cho barista/khách). Integration event là chuyện **giữa các service**. Bảng so sánh: `src/CyberCafe.IntegrationEvents/IIntegrationEvent.cs` và [docs/architecture.md](../architecture.md).

## Hệ quả

- (+) Không mất message khi Api/broker sập; không thu tiền 2 lần; không cộng điểm 2 lần.
- (+) Kiểm tra được không cần Docker: `MessagingFlowTests` (cùng transaction, rollback), `OutboxDispatcherTests` (gửi + đánh dấu, broker sập, nối trace), `PaymentMessagingTests` (Api thật + bus in-memory), `OrderPlacedConsumerTests` (MassTransit test harness).
- (−) At-least-once, không phải exactly-once: **mọi** consumer phải idempotent — quy tắc phải nhắc khi thêm consumer mới.
- (−) Bảng outbox/inbox lớn dần → cần job dọn dòng cũ (bài tập).
- (−) Nhiều instance Api cùng quét outbox có thể gửi trùng (vô hại nhờ idempotent, nhưng tốn) — chưa khóa dòng (`UPDLOCK, READPAST`).
- (−) Giữa lúc đặt và lúc có kết quả, đơn ở trạng thái "chờ thanh toán": màn hình quầy lọc `IsPaid`, trang khách hiện "Đang xử lý thanh toán".

## Khi nào xem lại

- Chạy nhiều instance Api → khóa dòng khi quét, hoặc chuyển sang MassTransit EF Outbox (đã có sẵn khóa + inbox + dọn dẹp).
- Thêm consumer thứ 3, 4… → gom kiểm tra inbox thành 1 pipeline behavior / MassTransit filter dùng chung thay vì viết trong từng handler.
- Khách hủy trong lúc chờ mà Payment đã thu → hiện chỉ log "cần hoàn tiền"; làm thật thì thêm `RefundRequested` (compensation / saga).
