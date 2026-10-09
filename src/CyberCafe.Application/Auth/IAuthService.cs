// ============================================================================
// IAuthService.cs — cổng (port) xác thực (Buổi 42–47 · 48).
// Buổi 42–47: AuthService là class cụ thể trong Api/Auth, dùng thẳng DbContext + User + BCrypt + JWT.
// Buổi 48: TOÀN BỘ phần cài đặt chuyển sang Infrastructure/Identity (đúng như ghi chú trong User.cs của b47:
//   "ai được đăng nhập, mật khẩu băm thế nào" là chuyện BẢO MẬT của hạ tầng, không phải nghiệp vụ quán).
//   Application chỉ giữ hợp đồng: vào RegisterRequest/LoginRequest, ra AuthResponse (Contracts).
// Controller phụ thuộc interface này → đổi sang ASP.NET Core Identity / Keycloak sau này không sửa controller.
// ============================================================================
using CyberCafe.Contracts.Auth;

namespace CyberCafe.Application.Auth;

// 👉 Bước 3 (b48.md)
/// <summary>Đăng ký, đăng nhập, làm mới token, đăng xuất.</summary>
public interface IAuthService
{
    /// <summary>Đăng ký khách hàng mới, trả luôn cặp token.</summary>
    /// <exception cref="Common.Exceptions.ConflictException">Email đã tồn tại (→ 409).</exception>
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);

    /// <summary>Đăng nhập bằng email + mật khẩu.</summary>
    /// <exception cref="Common.Exceptions.AuthenticationFailedException">Sai email hoặc mật khẩu (→ 401).</exception>
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);

    /// <summary>Đổi refresh token cũ lấy cặp token mới (rotation + phát hiện dùng lại).</summary>
    /// <exception cref="Common.Exceptions.AuthenticationFailedException">Token không hợp lệ / thu hồi / hết hạn (→ 401).</exception>
    Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>Thu hồi refresh token. Gọi nhiều lần không lỗi (idempotent).</summary>
    Task LogoutAsync(string refreshToken, CancellationToken ct = default);
}
