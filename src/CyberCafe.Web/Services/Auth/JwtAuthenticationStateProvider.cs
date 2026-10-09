// ============================================================================
// JwtAuthenticationStateProvider.cs — "người dùng hiện tại là ai" cho Blazor (Buổi 42–47).
// Blazor không tự biết ai đăng nhập; <AuthorizeView>, AuthorizeRouteView, [Authorize] đều HỎI provider này.
// Provider đọc access token trong AuthSession → giải mã PAYLOAD JWT → ClaimsPrincipal (tên, role).
// Cùng ý tưởng với lab blazor b34; khác ở chỗ token lấy từ AuthSession (có refresh token, rotation).
// Client KHÔNG kiểm chữ ký (không có khóa bí mật) — sửa token bằng tay chỉ đổi được GIAO DIỆN,
// gọi Api vẫn 401/403 vì chữ ký sai. Bảo mật thật nằm ở Api.
// ============================================================================
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using CyberCafe.Contracts.Auth;
using Microsoft.AspNetCore.Components.Authorization;

namespace CyberCafe.Web.Services.Auth;

/// <summary>AuthenticationStateProvider tự viết, dựa trên AuthSession.</summary>
public sealed class JwtAuthenticationStateProvider : AuthenticationStateProvider, IDisposable
{
    // Người dùng ẩn danh: ClaimsIdentity KHÔNG có authenticationType → IsAuthenticated = false
    private static readonly AuthenticationState Anonymous = new(new ClaimsPrincipal(new ClaimsIdentity()));
    private readonly AuthSession _session;

    /// <summary>Đăng ký nghe AuthSession.Changed để báo Blazor vẽ lại khi đăng nhập/đăng xuất.</summary>
    public JwtAuthenticationStateProvider(AuthSession session)
    {
        this._session = session;
        this._session.Changed += this.OnSessionChanged;
    }

    /// <inheritdoc />
    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        SessionData? data = await this._session.GetAsync();
        ClaimsPrincipal? principal = JwtClaimsReader.ToPrincipal(data?.AccessToken);
        return principal is null ? Anonymous : new AuthenticationState(principal);
    }

    // NotifyAuthenticationStateChanged → CascadingAuthenticationState đẩy state mới xuống mọi component.
    // ⚠️ Lỗi hay gặp: lưu token xong mà không báo → menu vẫn hiện "Đăng nhập" cho tới khi F5.
    private void OnSessionChanged() => this.NotifyAuthenticationStateChanged(this.GetAuthenticationStateAsync());

    /// <summary>Hủy đăng ký event (provider Scoped, AuthSession cũng Scoped — vẫn nên gỡ cho sạch).</summary>
    public void Dispose() => this._session.Changed -= this.OnSessionChanged;
}

/// <summary>Đọc claim từ phần payload của JWT (Base64Url → JSON).</summary>
public static class JwtClaimsReader
{
    /// <summary>
    /// Tạo ClaimsPrincipal từ token; null nếu token rỗng/hỏng. KHÔNG kiểm hạn ở đây:
    /// access token hết hạn vẫn hiển thị là "đã đăng nhập" vì AuthSession sẽ tự refresh khi gọi Api.
    /// </summary>
    public static ClaimsPrincipal? ToPrincipal(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            // JWT = header.payload.signature → phần [1] là payload
            string[] parts = token.Split('.');
            if (parts.Length != 3)
            {
                return null;
            }

            using JsonDocument payload = JsonDocument.Parse(Encoding.UTF8.GetString(Base64UrlDecode(parts[1])));
            List<Claim> claims = [];
            foreach (JsonProperty property in payload.RootElement.EnumerateObject())
            {
                // Nhiều role → mảng JSON → tách thành nhiều Claim cùng tên để IsInRole hoạt động
                if (property.Value.ValueKind == JsonValueKind.Array)
                {
                    claims.AddRange(property.Value.EnumerateArray().Select(v => new Claim(property.Name, v.ToString())));
                }
                else
                {
                    claims.Add(new Claim(property.Name, property.Value.ToString()));
                }
            }

            // nameType/roleType: Identity.Name đọc claim "name", IsInRole đọc claim "role" (khớp Api)
            // ⚠️ Lỗi hay gặp: bỏ 2 tham số này → .NET tìm claim URI dài mặc định → Name null, role không khớp.
            return new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt", AppClaimTypes.Name, AppClaimTypes.Role));
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            return null; // token rác → coi như chưa đăng nhập, không làm sập trang
        }
    }

    // Base64Url: '-' thay '+', '_' thay '/', bỏ '=' cuối → đổi ngược lại rồi bù '=' cho đủ bội số 4
    private static byte[] Base64UrlDecode(string input)
    {
        string base64 = input.Replace('-', '+').Replace('_', '/');
        base64 = (base64.Length % 4) switch
        {
            2 => base64 + "==",
            3 => base64 + "=",
            _ => base64
        };
        return Convert.FromBase64String(base64);
    }
}
