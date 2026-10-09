// ============================================================================
// PaymentApiClient.cs — typed HttpClient đọc giao dịch từ Payment service QUA GATEWAY (Buổi 54).
// Web không biết có bao nhiêu service phía sau: vẫn 1 BaseAddress (ApiBaseUrl = gateway), chỉ khác đường dẫn
// "payments" thay vì "api/...". Gateway kiểm JWT (chỉ Admin) rồi chuyển tới Payment service.
// PaymentSummary là DTO do Web TỰ khai báo (Payment service không chia sẻ project DTO): mỗi bên giữ bản của mình,
// chỉ cần cùng hình dạng JSON — đó là "hợp đồng" giữa client và service (consumer-driven contract).
// ============================================================================
using CyberCafe.Web.Models;
using CyberCafe.Web.Services.Auth;

namespace CyberCafe.Web.Services;

// 👉 Bước 7 (b54.md)
/// <summary>GET /payments (qua gateway, cần đăng nhập Admin).</summary>
public class PaymentApiClient(HttpClient http, AuthSession? session = null) : ApiClientBase(http, session)
{
    /// <summary>Các giao dịch mới nhất.</summary>
    /// <exception cref="ApiException">401/403 từ gateway, 404/503 khi không chạy dưới AppHost (không có Payment service).</exception>
    public Task<IReadOnlyList<PaymentSummary>> GetRecentAsync(int take = 20, CancellationToken ct = default) =>
        this.SendAsync<IReadOnlyList<PaymentSummary>>(new HttpRequestMessage(HttpMethod.Get, $"payments?take={take}"), ct);
}
