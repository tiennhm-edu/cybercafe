// ============================================================================
// PasswordHasher.cs — băm mật khẩu bằng BCrypt (Buổi 42–47 · 48 · lưu mật khẩu an toàn).
// Vì sao không SHA-256 cho mật khẩu? SHA quá NHANH: GPU thử hàng tỷ mật khẩu/giây.
// BCrypt: (1) SALT ngẫu nhiên mỗi lần băm → 2 người cùng mật khẩu vẫn khác hash;
//         (2) WORK FACTOR: chậm có chủ đích (2^11 vòng ≈ vài chục ms) → dò hàng loạt rất tốn kém.
// ============================================================================
using Microsoft.Extensions.Configuration;

namespace CyberCafe.Infrastructure.Identity;

/// <summary>
/// Băm / kiểm tra mật khẩu. Buổi 48: interface ở lại CÙNG tầng với nơi dùng (AuthService, DevAccountSeeder —
/// đều ở Infrastructure) → không cần đẩy lên Application. Port chỉ đặt ở Application khi Application cần gọi.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Băm mật khẩu — mỗi lần gọi ra 1 chuỗi KHÁC nhau (salt mới).</summary>
    string Hash(string password);

    /// <summary>So mật khẩu người dùng gõ với hash đã lưu.</summary>
    bool Verify(string password, string hash);
}

/// <summary>Cài đặt bằng BCrypt.Net-Next.</summary>
public class BCryptPasswordHasher(IConfiguration configuration) : IPasswordHasher
{
    // Work factor đọc từ cấu hình "Auth:BCryptWorkFactor" (mặc định 11).
    // Tăng 1 = chậm gấp đôi. Test đặt 4 (mức thấp nhất) để chạy nhanh — production KHÔNG hạ xuống.
    private readonly int _workFactor = configuration.GetValue("Auth:BCryptWorkFactor", 11);

    /// <inheritdoc />
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, this._workFactor);

    /// <inheritdoc />
    public bool Verify(string password, string hash)
    {
        // Verify đọc salt + work factor nằm sẵn trong chuỗi hash ("$2a$11$<salt><hash>") rồi băm lại để so
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false; // hash hỏng trong DB → coi như sai mật khẩu, không ném 500
        }
    }
}
