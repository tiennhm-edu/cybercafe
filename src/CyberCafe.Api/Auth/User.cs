// ============================================================================
// User.cs — tài khoản đăng nhập + refresh token (Buổi 42–47 · Authentication).
// Vì sao User nằm trong Api mà không trong Domain?
//   "Ai được đăng nhập, mật khẩu băm thế nào" là chuyện BẢO MẬT của ứng dụng, không phải
//   nghiệp vụ quán cà phê (Domain chỉ biết Customer đặt Order). Buổi 48 (Clean Architecture)
//   sẽ chuyển phần này vào Infrastructure.
// KHÔNG BAO GIỜ lưu mật khẩu gốc — chỉ lưu PasswordHash (BCrypt). Refresh token cũng chỉ lưu HASH.
// ============================================================================
using CyberCafe.Contracts.Auth;

namespace CyberCafe.Api.Auth;

/// <summary>Tài khoản đăng nhập (bảng Users).</summary>
public class User
{
    /// <summary>Khóa chính (IDENTITY).</summary>
    public int Id { get; set; }

    /// <summary>Email đăng nhập — duy nhất (unique index), lưu dạng chữ thường.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Chuỗi BCrypt dạng "$2a$11$..." — đã gồm salt + work factor, KHÔNG giải ngược được.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Họ tên hiển thị.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>SĐT (tùy chọn).</summary>
    public string? PhoneNumber { get; set; }

    /// <summary>Vai trò — lưu dạng chuỗi ("Customer").</summary>
    public UserRole Role { get; set; } = UserRole.Customer;

    /// <summary>Thời điểm tạo (UTC).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Các refresh token đã phát cho user này.</summary>
    public List<RefreshToken> RefreshTokens { get; set; } = [];
}

/// <summary>
/// Refresh token (bảng RefreshTokens): "vé" sống dài để xin access token mới mà không đăng nhập lại.
/// Mỗi lần dùng sẽ bị THU HỒI và thay bằng vé mới (rotation).
/// </summary>
public class RefreshToken
{
    /// <summary>Khóa chính.</summary>
    public int Id { get; set; }

    /// <summary>Chủ sở hữu.</summary>
    public int UserId { get; set; }

    /// <summary>Navigation tới User.</summary>
    public User? User { get; set; }

    /// <summary>SHA-256 (Base64) của token — lộ database thì kẻ tấn công vẫn không có token gốc để dùng.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>Thời điểm phát (UTC).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Hết hạn (UTC).</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>Thời điểm bị thu hồi (null = còn dùng được).</summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>Hash của token thay thế khi rotate — lần theo "chuỗi" token để phát hiện dùng lại.</summary>
    public string? ReplacedByTokenHash { get; set; }

    /// <summary>Còn hiệu lực: chưa thu hồi và chưa hết hạn.</summary>
    public bool IsActive(DateTime now) => this.RevokedAt is null && this.ExpiresAt > now;
}
