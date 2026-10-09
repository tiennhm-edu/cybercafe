// ============================================================================
// AuthDtos.cs — request/response của /api/auth/* (Buổi 42–47 · JWT + refresh token).
// Class có setter (không phải record) cho request → dùng làm Model của EditForm ở trang Login/Register.
// ============================================================================
using System.ComponentModel.DataAnnotations;

namespace CyberCafe.Contracts.Auth;

/// <summary>Đăng nhập bằng email + mật khẩu.</summary>
public class LoginRequest
{
    /// <summary>Email đăng nhập.</summary>
    [Required(ErrorMessage = "Vui lòng nhập email")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    public string Email { get; set; } = string.Empty;

    /// <summary>Mật khẩu (gửi qua HTTPS khi chạy thật; server chỉ so với hash BCrypt).</summary>
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
    public string Password { get; set; } = string.Empty;
}

/// <summary>Đăng ký tài khoản KHÁCH HÀNG (vai trò do server gán, client không được chọn).</summary>
public class RegisterRequest
{
    /// <summary>Email — duy nhất trong hệ thống.</summary>
    [Required(ErrorMessage = "Vui lòng nhập email")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    /// <summary>Mật khẩu ≥ 8 ký tự, có cả chữ và số.</summary>
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Mật khẩu từ 8 ký tự")]
    [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).+$", ErrorMessage = "Mật khẩu phải có cả chữ và số")]
    public string Password { get; set; } = string.Empty;

    /// <summary>Họ tên hiển thị.</summary>
    [Required(ErrorMessage = "Vui lòng nhập họ tên")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "Họ tên từ 2 đến 50 ký tự")]
    public string FullName { get; set; } = string.Empty;

    /// <summary>SĐT (tùy chọn) — điền sẵn ở trang đặt hàng.</summary>
    [RegularExpression(@"^0\d{9,10}$", ErrorMessage = "Số điện thoại gồm 10–11 chữ số, bắt đầu bằng 0")]
    public string? PhoneNumber { get; set; }
}

/// <summary>Body của /api/auth/refresh và /api/auth/logout.</summary>
/// <param name="RefreshToken">Refresh token đang giữ.</param>
public record RefreshRequest([Required] string RefreshToken);

/// <summary>Cặp token trả về sau login / register / refresh.</summary>
/// <param name="AccessToken">JWT sống ngắn (mặc định 15 phút) — gửi kèm mọi request: "Authorization: Bearer ...".</param>
/// <param name="AccessTokenExpiresAt">Hạn access token (UTC).</param>
/// <param name="RefreshToken">Chuỗi ngẫu nhiên sống dài (7 ngày) — chỉ dùng để xin cặp token mới.</param>
/// <param name="RefreshTokenExpiresAt">Hạn refresh token (UTC).</param>
/// <param name="UserId">Id người dùng.</param>
/// <param name="Email">Email.</param>
/// <param name="FullName">Họ tên.</param>
/// <param name="PhoneNumber">SĐT (có thể null).</param>
/// <param name="Role">Vai trò ("Customer" | "Barista" | "Admin").</param>
public record AuthResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    int UserId,
    string Email,
    string FullName,
    string? PhoneNumber,
    string Role);

/// <summary>Thông tin người đang đăng nhập (GET /api/auth/me) — đọc từ claim, không truy vấn DB.</summary>
/// <param name="UserId">Id người dùng.</param>
/// <param name="Email">Email.</param>
/// <param name="FullName">Họ tên.</param>
/// <param name="Role">Vai trò.</param>
public record MeResponse(int UserId, string Email, string FullName, string Role);
