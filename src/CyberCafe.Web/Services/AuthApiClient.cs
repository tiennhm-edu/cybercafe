// ============================================================================
// AuthApiClient.cs — typed HttpClient cho /api/auth/* (Buổi 42–47).
// KHÔNG gắn BearerTokenHandler: đăng nhập/đăng ký/refresh chưa có (hoặc đang hết hạn) access token.
// Nếu refresh cũng đi qua handler → handler gặp 401 lại gọi refresh → vòng lặp vô tận.
// ============================================================================
using CyberCafe.Contracts.Auth;

namespace CyberCafe.Web.Services;

/// <summary>Gọi các endpoint xác thực của Api.</summary>
public class AuthApiClient(HttpClient http) : ApiClientBase(http)
{
    /// <summary>POST /api/auth/login — 401 → ApiException("Sai email hoặc mật khẩu").</summary>
    public Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default) =>
        this.SendAsync<AuthResponse>(JsonRequest(HttpMethod.Post, "api/auth/login", request), ct);

    /// <summary>POST /api/auth/register — tạo tài khoản khách hàng.</summary>
    public Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default) =>
        this.SendAsync<AuthResponse>(JsonRequest(HttpMethod.Post, "api/auth/register", request), ct);

    /// <summary>POST /api/auth/refresh — đổi refresh token lấy cặp mới.</summary>
    public Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken ct = default) =>
        this.SendAsync<AuthResponse>(JsonRequest(HttpMethod.Post, "api/auth/refresh", new RefreshRequest(refreshToken)), ct);

    /// <summary>POST /api/auth/logout — thu hồi refresh token (lỗi mạng thì bỏ qua: phía client vẫn đăng xuất).</summary>
    public async Task LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        try
        {
            await this.SendAsync(JsonRequest(HttpMethod.Post, "api/auth/logout", new RefreshRequest(refreshToken)), ct);
        }
        catch (ApiException)
        {
            // Không thu hồi được trên server: refresh token tự hết hạn sau RefreshTokenDays
        }
    }
}
