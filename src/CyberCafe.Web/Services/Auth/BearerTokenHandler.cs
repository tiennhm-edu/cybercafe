// ============================================================================
// BearerTokenHandler.cs — DelegatingHandler gắn "Authorization: Bearer ..." (Buổi 42–47).
// DelegatingHandler = 1 "mắt xích" trong đường ống của HttpClient (giống middleware nhưng phía CLIENT):
//   MenuApiClient → BearerTokenHandler → (handler mạng thật) → Api
// Việc của mắt xích này: gắn token, gắn X-Correlation-Id, gặp 401 thì refresh 1 lần rồi gửi lại.
//
// ⚠️ Lỗi hay gặp (Blazor Server): inject AuthSession vào constructor của handler. IHttpClientFactory tạo
//    handler trong scope DI RIÊNG của nó, không phải scope của circuit → nhận AuthSession KHÁC (rỗng),
//    token luôn null. Lab webapi b47 né bằng cách gắn token trong typed client.
//    Ở đây: typed client (được tạo trong scope circuit) gửi AuthSession ĐÚNG kèm theo request qua
//    HttpRequestMessage.Options → handler đọc ra. Vừa giữ được DelegatingHandler, vừa đúng người dùng.
// ============================================================================
using System.Net;
using System.Net.Http.Headers;

namespace CyberCafe.Web.Services.Auth;

/// <summary>Gắn Bearer token của phiên hiện tại; 401 → refresh 1 lần → gửi lại.</summary>
public sealed class BearerTokenHandler : DelegatingHandler
{
    /// <summary>Key để typed client đính AuthSession vào request (xem ApiClientBase).</summary>
    public static readonly HttpRequestOptionsKey<AuthSession> SessionKey = new("CyberCafe.AuthSession");

    /// <summary>Header mã vết gửi kèm mỗi lời gọi Api (Api trả lại cùng mã, ghi trong log).</summary>
    public const string CorrelationHeader = "X-Correlation-Id";

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // 👉 Bước 14 (b47.md)
        if (!request.Headers.Contains(CorrelationHeader))
        {
            request.Headers.Add(CorrelationHeader, Guid.NewGuid().ToString("N"));
        }

        // Request không kèm session (AuthApiClient, hoặc test) → gửi thẳng, không token
        if (!request.Options.TryGetValue(SessionKey, out AuthSession? session))
        {
            return await base.SendAsync(request, cancellationToken);
        }

        string? token = await session.GetAccessTokenAsync(cancellationToken);
        if (token is null)
        {
            return await base.SendAsync(request, cancellationToken); // chưa đăng nhập → Api tự trả 401 nếu cần
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        // 401 dù token "còn hạn" theo đồng hồ Web (Api restart đổi key, lệch giờ...) → refresh rồi thử lại ĐÚNG 1 lần
        if (!await session.RefreshAsync(token, cancellationToken)
            || await session.GetAccessTokenAsync(cancellationToken) is not { } fresh)
        {
            return response; // refresh thất bại → trả 401 gốc, AuthSession đã đăng xuất
        }

        response.Dispose();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", fresh);
        // Gửi lại cùng HttpRequestMessage được vì ta đang ở TRONG đường ống handler (HttpClient chỉ cấm gửi lại
        // từ bên ngoài). JsonContent tự serialize lại body mỗi lần gửi.
        return await base.SendAsync(request, cancellationToken);
    }
}
