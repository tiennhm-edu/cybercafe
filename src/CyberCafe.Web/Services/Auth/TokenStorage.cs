// ============================================================================
// TokenStorage.cs — nơi cất phiên đăng nhập phía Web (Buổi 42–47).
// ProtectedSessionStorage = sessionStorage của trình duyệt nhưng dữ liệu được MÃ HÓA bằng
// Data Protection của server → người dùng mở DevTools không đọc/sửa được token.
// sessionStorage mất khi đóng tab (an toàn hơn localStorage), nhưng F5 vẫn còn.
// Tách interface ITokenStorage → unit test thay bằng bản lưu trong RAM (không cần trình duyệt).
// ============================================================================
using System.Security.Cryptography;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.JSInterop;

namespace CyberCafe.Web.Services.Auth;

/// <summary>Dữ liệu 1 phiên đăng nhập: cặp token + thông tin hiển thị.</summary>
/// <param name="AccessToken">JWT gửi kèm mỗi request.</param>
/// <param name="AccessTokenExpiresAt">Hạn access token (UTC).</param>
/// <param name="RefreshToken">Vé đổi token mới.</param>
/// <param name="RefreshTokenExpiresAt">Hạn refresh token (UTC) = hạn của cả phiên.</param>
/// <param name="FullName">Họ tên (điền sẵn form đặt hàng).</param>
/// <param name="PhoneNumber">SĐT (điền sẵn form đặt hàng).</param>
public record SessionData(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    string FullName,
    string? PhoneNumber);

/// <summary>Lưu / đọc / xóa phiên đăng nhập.</summary>
public interface ITokenStorage
{
    /// <summary>Lưu phiên.</summary>
    Task SaveAsync(SessionData data);

    /// <summary>Đọc phiên đã lưu (null nếu chưa có / không đọc được).</summary>
    Task<SessionData?> LoadAsync();

    /// <summary>Xóa phiên.</summary>
    Task ClearAsync();
}

/// <summary>Cài đặt bằng ProtectedSessionStorage (cần JS interop → chỉ dùng khi component đã interactive).</summary>
public class ProtectedSessionTokenStorage(ProtectedSessionStorage storage, ILogger<ProtectedSessionTokenStorage> logger) : ITokenStorage
{
    private const string Key = "cybercafe.session";

    /// <inheritdoc />
    public async Task SaveAsync(SessionData data)
    {
        try
        {
            await storage.SetAsync(Key, data);
        }
        catch (Exception ex) when (ex is JSDisconnectedException or InvalidOperationException)
        {
            // Tab vừa đóng / chưa có kết nối JS: phiên trong RAM vẫn dùng được, chỉ mất khi F5
            logger.LogDebug(ex, "Không lưu được phiên vào sessionStorage");
        }
    }

    /// <inheritdoc />
    public async Task<SessionData?> LoadAsync()
    {
        try
        {
            ProtectedBrowserStorageResult<SessionData> result = await storage.GetAsync<SessionData>(Key);
            return result.Success ? result.Value : null;
        }
        catch (CryptographicException)
        {
            // Khóa Data Protection đổi (restart Web) → dữ liệu cũ không giải mã được → coi như chưa đăng nhập
            await this.ClearAsync();
            return null;
        }
        catch (Exception ex) when (ex is JSDisconnectedException or InvalidOperationException)
        {
            // ⚠️ Lỗi hay gặp: gọi lúc PRERENDER (chưa có trình duyệt) → InvalidOperationException.
            //    Vì vậy App.razor tắt prerender từ buổi 42–47.
            return null;
        }
    }

    /// <inheritdoc />
    public async Task ClearAsync()
    {
        try
        {
            await storage.DeleteAsync(Key);
        }
        catch (Exception ex) when (ex is JSDisconnectedException or InvalidOperationException)
        {
            logger.LogDebug(ex, "Không xóa được phiên trong sessionStorage");
        }
    }
}
