// ============================================================================
// AuthService.cs — đăng ký, đăng nhập, làm mới token, đăng xuất (Buổi 42–47 · 48).
// Refresh token ROTATION: mỗi refresh token chỉ dùng được 1 LẦN, dùng xong đổi vé mới.
// REUSE DETECTION: có người dùng lại vé ĐÃ bị thay → dấu hiệu bị đánh cắp
//   → thu hồi TOÀN BỘ vé của user (kẻ trộm lẫn chủ thật đều phải đăng nhập lại).
// Dùng DbContext trực tiếp (không Repository/UoW) — lý do xem docs/adr/0001-dbcontext-truc-tiep.md.
// Buổi 48: chuyển từ Api/Auth sang Infrastructure/Identity và implement IAuthService (Application).
//   Vẫn dùng DbContext trực tiếp: cả class này ĐÃ là hạ tầng (cùng tầng với EF Core) — ADR 0002 giải thích
//   vì sao repository chỉ cần cho code nằm ở Application, không cần cho code nằm ở Infrastructure.
// ============================================================================
using CyberCafe.Application.Auth;
using CyberCafe.Application.Common.Exceptions;
using CyberCafe.Contracts.Auth;
using CyberCafe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CyberCafe.Infrastructure.Identity;

// 👉 Bước 3 (b48.md)
/// <summary>Nghiệp vụ xác thực (cài đặt <see cref="IAuthService"/>).</summary>
public class AuthService(
    CyberCafeDbContext db,
    ITokenService tokens,
    IPasswordHasher hasher,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider clock,
    ILogger<AuthService> logger) : IAuthService
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    // 👉 Bước 3 (b47.md)
    /// <summary>Đăng ký khách hàng mới, trả luôn cặp token (đăng nhập ngay).</summary>
    /// <exception cref="ConflictException">Email đã tồn tại (→ 409).</exception>
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        string email = NormalizeEmail(request.Email);
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
        {
            // Unique index trên Users.Email vẫn chặn trường hợp 2 request cùng lúc lọt qua dòng kiểm tra này
            throw new ConflictException("Email đã được đăng ký");
        }

        User user = new()
        {
            Email = email,
            PasswordHash = hasher.Hash(request.Password),
            FullName = request.FullName.Trim(),
            PhoneNumber = request.PhoneNumber,
            // ⚠️ Lỗi hay gặp: cho client gửi "role" khi đăng ký → ai cũng tự phong Admin. Role do SERVER gán.
            Role = UserRole.Customer,
            CreatedAt = this.Now,
        };
        db.Users.Add(user);
        // Chưa SaveChanges ở đây: IssueTokensAsync thêm RefreshToken rồi lưu CẢ HAI trong 1 transaction
        return await this.IssueTokensAsync(user, replacing: null, ct); // 1 SaveChanges: Users + RefreshTokens
    }

    /// <summary>Đăng nhập: so mật khẩu với hash BCrypt.</summary>
    /// <exception cref="AuthenticationFailedException">Sai email hoặc mật khẩu (→ 401).</exception>
    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        // Email lưu chữ thường → "Admin@CyberCafe.local" vẫn đăng nhập được
        string email = NormalizeEmail(request.Email);
        User? user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        // CÙNG 1 thông báo cho "sai email" và "sai mật khẩu" → kẻ tấn công không dò được email nào có tài khoản
        if (user is null || !hasher.Verify(request.Password, user.PasswordHash))
        {
            throw new AuthenticationFailedException("Sai email hoặc mật khẩu");
        }

        return await this.IssueTokensAsync(user, replacing: null, ct);
    }

    // 👉 Bước 4 (b47.md)
    /// <summary>Đổi refresh token cũ lấy cặp token mới (token cũ bị thu hồi).</summary>
    /// <exception cref="AuthenticationFailedException">Token không tồn tại / đã thu hồi / hết hạn (→ 401).</exception>
    public async Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        // DB chỉ có HASH → băm token client gửi lên rồi tìm theo hash
        string hash = tokens.Hash(refreshToken);
        RefreshToken? stored = await db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct)
            ?? throw new AuthenticationFailedException("Refresh token không hợp lệ");

        if (stored.RevokedAt is not null)
        {
            // Vé đã dùng rồi mà vẫn có người đem ra dùng → khóa cả "họ" vé của user này
            logger.LogWarning("Phát hiện dùng lại refresh token đã thu hồi của user {UserId}", stored.UserId);
            await this.RevokeAllAsync(stored.UserId, ct);
            throw new AuthenticationFailedException("Refresh token đã bị thu hồi");
        }

        if (stored.ExpiresAt <= this.Now)
        {
            throw new AuthenticationFailedException("Refresh token đã hết hạn");
        }

        return await this.IssueTokensAsync(stored.User!, replacing: stored, ct);
    }

    /// <summary>Đăng xuất: thu hồi refresh token (access token tự hết hạn sau ≤ 15 phút). Gọi nhiều lần không lỗi.</summary>
    public async Task LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        string hash = tokens.Hash(refreshToken);
        RefreshToken? stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is null || stored.RevokedAt is not null)
        {
            return; // idempotent
        }

        stored.RevokedAt = this.Now;
        await db.SaveChangesAsync(ct);
    }

    // Thu hồi MỌI refresh token còn hiệu lực của user (dùng khi phát hiện vé bị dùng lại)
    private async Task RevokeAllAsync(int userId, CancellationToken ct)
    {
        DateTime now = this.Now;
        List<RefreshToken> active = await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now)
            .ToListAsync(ct);
        foreach (RefreshToken token in active)
        {
            token.RevokedAt = now;
        }

        await db.SaveChangesAsync(ct);
    }

    // Tạo access + refresh token; nếu đang rotate thì thu hồi vé cũ trong CÙNG lần SaveChanges (1 transaction).
    private async Task<AuthResponse> IssueTokensAsync(User user, RefreshToken? replacing, CancellationToken ct)
    {
        (string accessToken, DateTime accessExpires) = tokens.CreateAccessToken(user);
        string refreshToken = tokens.CreateRefreshToken();
        string refreshHash = tokens.Hash(refreshToken);
        DateTime refreshExpires = this.Now.AddDays(jwtOptions.Value.RefreshTokenDays);

        user.RefreshTokens.Add(new RefreshToken
        {
            TokenHash = refreshHash, // chỉ lưu HASH; token gốc chỉ trả về cho client đúng 1 lần
            CreatedAt = this.Now,
            ExpiresAt = refreshExpires,
        });

        if (replacing is not null)
        {
            replacing.RevokedAt = this.Now;
            replacing.ReplacedByTokenHash = refreshHash;
        }

        await db.SaveChangesAsync(ct);
        return new AuthResponse(accessToken, accessExpires, refreshToken, refreshExpires,
            user.Id, user.Email, user.FullName, user.PhoneNumber, user.Role.ToString());
    }

    /// <summary>Email so sánh không phân biệt hoa/thường → lưu và tìm theo chữ thường.</summary>
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
