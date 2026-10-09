# ADR 0004 — Message broker: RabbitMQ qua MassTransit **8.x** (Apache-2.0), không dùng Kafka, không nâng lên MassTransit 9

- **Trạng thái:** Chấp nhận (tag `b55-microservice`)
- **Bối cảnh buổi học:** 54–55 (tách service, .NET Aspire, message broker)
- **Quan hệ:** xây tiếp trên [ADR 0003](0003-cqrs-dispatcher-tu-viet.md) (domain event trong 1 tiến trình); đi cùng [ADR 0005](0005-outbox-idempotent-consumer.md) (Outbox + idempotent consumer)

## Bối cảnh

Từ `b55`, thanh toán tách ra **Payment service** (`CyberCafe.Payment.Api`, database riêng). Order service (`CyberCafe.Api`) và Payment phải báo cho nhau:

```
Api ── OrderPlacedIntegrationEvent ──► Payment ── PaymentCompleted / PaymentFailed ──► Api
```

Gọi HTTP đồng bộ (Api → `POST payment/charge`) thì Api phải chờ Payment; Payment tắt = không đặt được đơn; Payment chậm = khách chờ. Cần **giao tiếp bất đồng bộ qua message broker**. Lớp chỉ có 2 buổi → chọn **1** broker, **1** thư viện, giải thích được trong 1 buổi.

## Phương án broker

| Tiêu chí | **RabbitMQ** | Kafka |
|----------|--------------|-------|
| Mô hình | Queue + exchange: message được **giao** cho consumer, ack xong thì xóa | Log bền vững: message **nằm lại**, consumer tự giữ offset, đọc lại được |
| Hợp với | Lệnh/sự kiện nghiệp vụ giữa vài service, cần retry/dead-letter | Luồng sự kiện khối lượng lớn, event sourcing, phân tích, phát lại lịch sử |
| Chạy dev | 1 container ~150 MB, UI quản lý có sẵn (`-management`) | Cần broker (+ KRaft/ZooKeeper), cấu hình partition/replication — nặng cho máy học viên |
| Aspire | `AddRabbitMQ(...).WithManagementPlugin()` | `AddKafka(...)` (có), nhưng thư viện .NET phía client ít "batteries" hơn |
| Retry / dead-letter | Có sẵn (MassTransit: `UseMessageRetry`, queue `_error`) | Tự xây (topic retry/DLQ) |
| Thứ tự | Theo queue (đủ dùng ở đây) | Theo partition (mạnh hơn, phức tạp hơn) |

**Chọn RabbitMQ.** CyberCafe có vài message nghiệp vụ/ngày, cần "giao đúng 1 consumer, lỗi thì thử lại, hỏng hẳn thì để riêng ra" — đúng sở trường của RabbitMQ. Kafka đáng giá khi cần **lưu và phát lại** luồng sự kiện lớn (đề xuất bài tập đọc thêm, không làm trong 2 buổi).

## Phương án thư viện .NET

| Phương án | Giấy phép | Ưu | Nhược |
|-----------|-----------|----|-------|
| `RabbitMQ.Client` 7 thuần | Apache-2.0 OR MPL-2.0 | Thấy rõ AMQP (exchange, binding, ack) | Tự viết khai báo topology, serialize, retry, scope DI cho mỗi message, truyền trace context — ~300 dòng hạ tầng trước khi có dòng nghiệp vụ đầu tiên |
| **MassTransit 8.5.11** | **Apache-2.0** | Consumer = 1 class `IConsumer<T>`; DI scope / message; retry; queue `_error`; topology tự sinh theo kiểu message; **test harness trong bộ nhớ** (test không cần Docker); tự phát span OpenTelemetry và truyền `traceparent` qua header | Thêm 1 lớp trừu tượng; v9 đổi giấy phép (xem dưới) |
| MassTransit 9.x | **Thương mại** (cần license key) | Bản mới nhất | Không phù hợp repo học công khai / học viên dùng tự do |
| NServiceBus, Brighter… | Thương mại / MIT | — | Ngoài phạm vi khóa |

**Chọn MassTransit 8.x, khóa version ở `8.5.11`** trong `Directory.Packages.props`.

- Đã đọc nuspec trên nuget.org: `MassTransit`, `MassTransit.Abstractions`, `MassTransit.RabbitMQ` 8.5.11 đều `<license type="expression">Apache-2.0</license>`; bản **9.x** trỏ license tới trang thương mại của nhà phát triển. `RabbitMQ.Client` 7.2.2 (phụ thuộc): `Apache-2.0 OR MPL-2.0`.
- Theo thông báo của nhóm phát triển MassTransit (2025), v8 giữ giấy phép Apache-2.0 và tiếp tục được vá lỗi/bảo mật tới khoảng cuối năm 2026 — giảng viên kiểm tra lại trang dự án trước mỗi khóa. Sau mốc đó: đánh giá lại (xem "Khi nào xem lại").
- ⚠️ `dotnet add package MassTransit` **không ghi version** sẽ lấy 9.x → comment cảnh báo nằm ngay trong `Directory.Packages.props`. Central Package Management bảo đảm mọi project dùng cùng 8.5.11.

## Quyết định chi tiết

1. **Hợp đồng message** ở project riêng `CyberCafe.IntegrationEvents`: record `sealed`, chỉ kiểu nguyên thủy, **không** tham chiếu Domain/Contracts/MassTransit (architecture test `ServiceBoundaryTests`). MassTransit đặt tên exchange theo kiểu message (`CyberCafe.IntegrationEvents:OrderPlacedIntegrationEvent`) → 2 service phải dùng **chung** kiểu này.
2. **Tên queue có tiền tố service**: `KebabCaseEndpointNameFormatter("ordering")` / `("payment")` → `ordering-payment-completed`, `payment-order-placed`. Hai service không bao giờ tranh nhau 1 queue.
3. **Application không biết MassTransit**: consumer (`PaymentCompletedConsumer`…) nằm ở Infrastructure, chỉ đổi message → command → `ISender` (giống controller). Luật nằm ở command handler — test bằng port giả.
4. **Retry**: `UseMessageRetry` 3 lần (200 ms, 1 s, 5 s); bỏ qua `DomainException`/`ValidationException` (thử lại vô ích); hết lượt → queue `<tên>_error`.
5. **Không có broker** (test, chạy Api lẻ): `UsingInMemory` — cùng consumer, cùng pipeline, chỉ khác transport. `ConnectionStrings:messaging` (Aspire bơm) có giá trị → `UsingRabbitMq`.
6. **Không dùng outbox có sẵn của MassTransit** (`AddEntityFrameworkOutbox`): tự viết outbox ~150 dòng để lớp hiểu cơ chế (cùng tinh thần dispatcher tự viết ở ADR 0003) — chi tiết ở ADR 0005.

## Hệ quả

- (+) Api không chờ Payment: `POST /api/orders` trả 201 ngay (đơn "đang xử lý thanh toán"); Payment tắt vài phút → message nằm chờ trong queue, bật lại là xử lý tiếp.
- (+) Test không cần Docker: `ITestHarness` (Payment.Tests) và bus in-memory (Api.Tests).
- (+) Trace xuyên service miễn phí: span MassTransit `send` / `receive` / `process` nối với span HTTP của gateway và Api trên Aspire dashboard.
- (−) Thêm 1 thư viện lớn có lịch sử đổi giấy phép → **phải kiểm tra license mỗi lần nâng version**.
- (−) Bất đồng bộ = khách thấy kết quả thanh toán chậm 1–2 giây; giao diện phải có trạng thái "đang xử lý" (trang `/orders/{id}` đã làm).

## Khi nào xem lại

- Hết vòng đời hỗ trợ của MassTransit 8 (cuối 2026) hoặc có CVE không được vá → đổi sang `RabbitMQ.Client` thuần (consumer hiện chỉ 2 class mỏng — đổi transport không chạm Application) hoặc 1 thư viện Apache/MIT khác.
- Cần lưu và phát lại luồng sự kiện (báo cáo thời gian thực, event sourcing) → cân nhắc Kafka cho luồng đó, RabbitMQ vẫn giữ cho lệnh nghiệp vụ.
