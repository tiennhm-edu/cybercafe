// ============================================================================
// TokenService.cs — phát access token (JWT) và refresh token (Buổi 42–47).
// JWT = header.payload.signature (Base64Url). Payload chứa claims: sub, email, name, role, exp...
// Chữ ký HMAC-SHA256 bằng Jwt:Key → Api kiểm tra token KHÔNG bị sửa mà không cần tra database.
// ⚠️ JWT chỉ được KÝ, không MÃ HÓA: ai có token cũng đọc được payload (dán vào jwt.io)
//    → không bỏ mật khẩu, số thẻ... vào claim.
// ============================================================================
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CyberCafe.Contracts.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CyberCafe.Api.Auth;

/// <summary>Tạo access token / refresh token.</summary>
public interface ITokenService
{
    /// <summary>JWT ký HMAC-SHA256, hết hạn sau Jwt:AccessTokenMinutes phút.</summary>
    (string Token, DateTime ExpiresAt) CreateAccessToken(User user);

    /// <summary>Chuỗi ngẫu nhiên 64 byte (Base64Url) — KHÔNG phải JWT, chỉ là "vé" tra trong DB.</summary>
    string CreateRefreshToken();

    /// <summary>SHA-256 của refresh token để lưu DB.</summary>
    string Hash(string refreshToken);
}

/// <summary>Cài đặt bằng Microsoft.IdentityModel.JsonWebTokens.</summary>
public class TokenService(IOptions<JwtOptions> options, TimeProvider clock) : ITokenService
{
    private readonly JwtOptions _options = options.Value;

    /// <inheritdoc />
    public (string Token, DateTime ExpiresAt) CreateAccessToken(User user)
    {
        DateTime now = clock.GetUtcNow().UtcDateTime;
        DateTime expires = now.AddMinutes(this._options.AccessTokenMinutes);

        // 👉 Bước 2 (b47.md): claims = "thông tin về người dùng" mà server cam kết bằng chữ ký
        List<Claim> claims =
        [
            new(AppClaimTypes.UserId, user.Id.ToString()),
            new(AppClaimTypes.Email, user.Email),
            new(AppClaimTypes.Name, user.FullName),
            new(AppClaimTypes.Role, user.Role.ToString()),                 // [Authorize(Roles = "Admin")] đọc claim này
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),  // id riêng của từng token
        ];

        SecurityTokenDescriptor descriptor = new()
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = this._options.Issuer,
            Audience = this._options.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(this._options.Key)),
                SecurityAlgorithms.HmacSha256),
        };

        return (new JsonWebTokenHandler().CreateToken(descriptor), expires);
    }

    // RandomNumberGenerator: ngẫu nhiên AN TOÀN MẬT MÃ. ⚠️ Không dùng new Random() cho token (đoán được).
    /// <inheritdoc />
    public string CreateRefreshToken() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));

    // SHA-256 đủ cho refresh token (chuỗi ngẫu nhiên 512 bit, không dò được); mật khẩu thì phải BCrypt (xem PasswordHasher).
    /// <inheritdoc />
    public string Hash(string refreshToken) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
}
