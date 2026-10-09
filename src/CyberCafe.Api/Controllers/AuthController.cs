// ============================================================================
// AuthController.cs — /api/auth/* (Buổi 42–47 · JWT + refresh token + rate limiting).
//   POST /api/auth/register   201 | 400 | 409 (email trùng) | 429
//   POST /api/auth/login      200 | 401 | 429 (quá nhiều lần thử)
//   POST /api/auth/refresh    200 | 401
//   POST /api/auth/logout     204
//   GET  /api/auth/me         200 | 401
// Controller rất mỏng: lỗi (401/409) do AuthService ném exception, DomainExceptionHandler đổi thành HTTP.
// ============================================================================
using CyberCafe.Api.Auth;
using CyberCafe.Contracts.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CyberCafe.Api.Controllers;

/// <summary>Đăng ký / đăng nhập / làm mới token.</summary>
[ApiController]
[Route("api/auth")]
public class AuthController(AuthService auth) : ControllerBase
{
    /// <summary>Tên policy rate limit cho đăng nhập / đăng ký (khai báo trong Program.cs).</summary>
    public const string LoginRateLimit = "login";

    /// <summary>Đăng ký tài khoản khách hàng, trả luôn cặp token.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(LoginRateLimit)] // chống tạo tài khoản rác hàng loạt
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        AuthResponse response = await auth.RegisterAsync(request, ct);
        return this.CreatedAtAction(nameof(this.Me), null, response);
    }

    // 👉 Bước 10 (b47.md): [EnableRateLimiting] — quá N lần / phút từ 1 IP → 429, chống dò mật khẩu (brute force)
    /// <summary>Đăng nhập bằng email + mật khẩu.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(LoginRateLimit)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public Task<AuthResponse> Login(LoginRequest request, CancellationToken ct) => auth.LoginAsync(request, ct);

    /// <summary>Đổi refresh token lấy cặp token mới (token cũ bị thu hồi).</summary>
    [HttpPost("refresh")]
    [AllowAnonymous] // access token có thể ĐÃ hết hạn → endpoint này không được đòi access token
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public Task<AuthResponse> Refresh(RefreshRequest request, CancellationToken ct) => auth.RefreshAsync(request.RefreshToken, ct);

    /// <summary>Đăng xuất: thu hồi refresh token.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(RefreshRequest request, CancellationToken ct)
    {
        await auth.LogoutAsync(request.RefreshToken, ct);
        return this.NoContent();
    }

    /// <summary>Thông tin người đang đăng nhập — đọc từ claim của access token (không truy vấn DB).</summary>
    [HttpGet("me")]
    [Authorize] // thiếu / sai / hết hạn token → 401
    [ProducesResponseType<MeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public MeResponse Me() => new(
        this.User.GetUserId()!.Value,
        this.User.FindFirst(AppClaimTypes.Email)!.Value,
        this.User.Identity!.Name!,
        this.User.FindFirst(AppClaimTypes.Role)!.Value);
}
