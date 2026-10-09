// ============================================================================
// JwtOptions.cs — cấu hình JWT theo Options pattern (Buổi 42–47).
// Đọc từ section "Jwt" (appsettings / user-secrets / biến môi trường Jwt__Key).
// ⚠️ Lỗi hay gặp: commit Jwt:Key THẬT lên GitHub. Repo này chỉ có key GIẢ trong appsettings.Development.json;
//    môi trường thật đặt key bằng user-secrets hoặc biến môi trường.
// ============================================================================
namespace CyberCafe.Api.Auth;

/// <summary>Thông số phát hành / kiểm tra JWT.</summary>
public class JwtOptions
{
    /// <summary>Tên section trong cấu hình.</summary>
    public const string SectionName = "Jwt";

    /// <summary>Ai phát hành token (claim "iss").</summary>
    public string Issuer { get; set; } = "CyberCafe.Api";

    /// <summary>Token dành cho ai (claim "aud").</summary>
    public string Audience { get; set; } = "CyberCafe.Clients";

    /// <summary>Khóa bí mật ký HMAC-SHA256 — tối thiểu 32 byte (256 bit).</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Access token sống NGẮN: lộ thì thiệt hại ít (không thu hồi được JWT đã phát).</summary>
    public int AccessTokenMinutes { get; set; } = 15;

    /// <summary>Refresh token sống dài, nằm trong DB nên thu hồi được.</summary>
    public int RefreshTokenDays { get; set; } = 7;
}
