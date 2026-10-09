// ============================================================================
// IIntegrationEvent.cs — sự kiện đi QUA RANH GIỚI service (Buổi 55 · integration event).
// So sánh với domain event (Buổi 50, CyberCafe.Domain/Common/IDomainEvent.cs):
//   | Domain event (OrderPaid)            | Integration event (OrderPlacedIntegrationEvent)       |
//   |-------------------------------------|--------------------------------------------------------|
//   | Trong 1 tiến trình, 1 service       | Giữa các service, qua broker (RabbitMQ)                |
//   | Giữ THAM CHIẾU tới aggregate Order  | Chỉ dữ liệu nguyên thủy, tuần tự hóa được (JSON)       |
//   | Phát sau commit, "cố gắng hết sức"  | Ghi vào OUTBOX cùng transaction → chắc chắn tới nơi    |
//   | Đổi thoải mái (nội bộ)              | Là HỢP ĐỒNG công khai → chỉ THÊM field, không đổi/xóa  |
// EventId là "chứng minh thư" của message: consumer dùng nó để bỏ qua bản trùng (idempotent consumer).
// ============================================================================
namespace CyberCafe.IntegrationEvents;

// 👉 Bước 1 (b55.md)
/// <summary>Đánh dấu 1 integration event (message giữa các service).</summary>
public interface IIntegrationEvent
{
    /// <summary>Id duy nhất của sự kiện — giữ NGUYÊN khi gửi lại (retry, outbox gửi lần 2) để bên nhận lọc trùng.</summary>
    Guid EventId { get; }

    /// <summary>Thời điểm sự kiện xảy ra (UTC — các service có thể chạy ở múi giờ khác nhau).</summary>
    DateTime OccurredAtUtc { get; }
}
