// ============================================================================
// OrderingSettings.cs — chọn CÁCH THANH TOÁN khi đặt hàng (Buổi 55).
//   InProcess  — như b53: PlaceOrder gọi order.Pay(...) ngay, 1 request là xong. Dùng khi chạy Api lẻ
//                (docker compose + dotnet run) và cho 209 test cũ (không cần broker).
//   Messaging  — b55: PlaceOrder chỉ lưu đơn + ghi OrderPlacedIntegrationEvent vào outbox; Payment service
//                trả lời qua RabbitMQ; Api cập nhật đơn khi nhận PaymentCompleted / PaymentFailed.
// AppHost đặt biến môi trường Payments__Flow=Messaging. Infrastructure đọc cấu hình và đăng ký đè giá trị mặc định.
// Vì sao không bỏ hẳn InProcess? Lớp học cần chạy được mọi tag cũ không Docker; test tích hợp không cần RabbitMQ.
// ============================================================================
namespace CyberCafe.Application.Orders;

/// <summary>Đơn được thanh toán theo cách nào.</summary>
public enum PaymentFlow
{
    /// <summary>Thanh toán ngay trong request đặt hàng (b40–b53).</summary>
    InProcess,

    /// <summary>Thanh toán bất đồng bộ qua Payment service + RabbitMQ (b55).</summary>
    Messaging,
}

// 👉 Bước 2 (b55.md)
/// <summary>Cấu hình luồng đặt hàng (đăng ký Singleton; mặc định InProcess).</summary>
/// <param name="PaymentFlow">Cách thanh toán.</param>
public sealed record OrderingSettings(PaymentFlow PaymentFlow = PaymentFlow.InProcess)
{
    /// <summary>Giá trị mặc định (giữ hành vi b53).</summary>
    public static OrderingSettings Default { get; } = new();
}
