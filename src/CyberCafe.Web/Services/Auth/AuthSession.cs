// ============================================================================
// AuthSession.cs — trạng thái đăng nhập của 1 người dùng (1 circuit) (Buổi 42–47).
// Scoped trong Blazor Server = sống theo CIRCUIT (1 tab), không phải theo HTTP request như Web API.
// Trách nhiệm:
//   - Giữ cặp token trong RAM + đồng bộ xuống ITokenStorage (F5 không mất phiên).
//   - Cấp access token còn hạn cho BearerTokenHandler / HubConnection; sắp hết hạn → tự refresh.
//   - Báo sự kiện Changed khi đăng nhập/đăng xuất → AuthenticationStateProvider vẽ lại giao diện.
// ============================================================================
using System.Net;
using CyberCafe.Contracts.Auth;

namespace CyberCafe.Web.Services.Auth;

/// <summary>Phiên đăng nhập của circuit hiện tại.</summary>
public class AuthSession(ITokenStorage storage, AuthApiClient authApi, TimeProvider clock, ILogger<AuthSession> logger)
{
    // Refresh sớm 30 giây trước hạn → request đang bay không bị hết hạn giữa đường
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromSeconds(30);

    // ⚠️ Lỗi hay gặp: 2 request cùng gặp 401 → cùng refresh với CÙNG 1 refresh token → lần thứ 2 bị Api coi
    //    là "dùng lại token đã thu hồi" → thu hồi cả phiên. SemaphoreSlim cho phép 1 lần refresh tại 1 thời điểm.
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private SessionData? _data;
    private bool _loaded;

    /// <summary>Bắn ra khi đăng nhập / đăng xuất / phiên hết hạn.</summary>
    public event Action? Changed;

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>Phiên hiện tại (null = chưa đăng nhập hoặc refresh token đã hết hạn).</summary>
    public async Task<SessionData?> GetAsync()
    {
        if (!this._loaded)
        {
            this._data = await storage.LoadAsync(); // lần đầu trong circuit: đọc sessionStorage
            this._loaded = true;
        }

        if (this._data is not null && this._data.RefreshTokenExpiresAt <= this.Now)
        {
            this._data = null; // hết hạn cả phiên → coi như đã đăng xuất
            await storage.ClearAsync();
        }

        return this._data;
    }

    // 👉 Bước 13 (b47.md)
    /// <summary>Lưu phiên sau khi đăng nhập / đăng ký thành công.</summary>
    public async Task SignInAsync(AuthResponse response)
    {
        this._data = new SessionData(response.AccessToken, response.AccessTokenExpiresAt, response.RefreshToken,
            response.RefreshTokenExpiresAt, response.FullName, response.PhoneNumber);
        this._loaded = true;
        await storage.SaveAsync(this._data);
        this.Changed?.Invoke(); // → JwtAuthenticationStateProvider → <AuthorizeView> vẽ lại NGAY, không cần F5
    }

    /// <summary>Đăng xuất phía client (gọi Api thu hồi refresh token là việc của AuthApiClient).</summary>
    public async Task SignOutAsync()
    {
        this._data = null;
        this._loaded = true;
        await storage.ClearAsync();
        this.Changed?.Invoke();
    }

    /// <summary>
    /// Access token còn hạn để gắn vào request / hub; sắp hết hạn thì refresh trước.
    /// null = chưa đăng nhập hoặc refresh thất bại.
    /// </summary>
    public async Task<string?> GetAccessTokenAsync(CancellationToken ct = default)
    {
        SessionData? data = await this.GetAsync();
        if (data is null)
        {
            return null;
        }

        if (data.AccessTokenExpiresAt - this.Now > RefreshMargin)
        {
            return data.AccessToken;
        }

        return await this.RefreshAsync(data.AccessToken, ct) ? this._data?.AccessToken : null;
    }

    /// <summary>
    /// Đổi refresh token lấy cặp token mới. <paramref name="staleAccessToken"/> = token vừa bị từ chối:
    /// nếu trong lúc chờ khóa đã có ai refresh xong (token khác rồi) thì dùng luôn, không refresh lần nữa.
    /// </summary>
    public async Task<bool> RefreshAsync(string staleAccessToken, CancellationToken ct = default)
    {
        await this._refreshLock.WaitAsync(ct);
        try
        {
            if (this._data is null)
            {
                return false;
            }

            if (this._data.AccessToken != staleAccessToken)
            {
                return true; // request khác đã refresh giúp
            }

            AuthResponse response = await authApi.RefreshAsync(this._data.RefreshToken, ct);
            this._data = this._data with
            {
                AccessToken = response.AccessToken,
                AccessTokenExpiresAt = response.AccessTokenExpiresAt,
                RefreshToken = response.RefreshToken,           // ROTATION: vé cũ đã bị Api thu hồi
                RefreshTokenExpiresAt = response.RefreshTokenExpiresAt,
            };
            await storage.SaveAsync(this._data);
            return true;
        }
        catch (ApiException ex) when (ex.Status == HttpStatusCode.Unauthorized)
        {
            // Refresh token hết hạn / bị thu hồi → buộc đăng nhập lại
            logger.LogInformation("Refresh token bị từ chối — đăng xuất phiên hiện tại");
            this._data = null;
            await storage.ClearAsync();
            this.Changed?.Invoke();
            return false;
        }
        finally
        {
            this._refreshLock.Release();
        }
    }
}
