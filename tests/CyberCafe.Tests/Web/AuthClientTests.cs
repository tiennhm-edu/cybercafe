// ============================================================================
// AuthClientTests.cs — phần đăng nhập phía Web (Buổi 42–47): BearerTokenHandler, AuthSession,
// JwtAuthenticationStateProvider. Không cần Api thật: FakeHttpHandler đóng vai Api.
// ============================================================================
using System.Net;
using System.Security.Claims;
using System.Text;
using CyberCafe.Contracts.Auth;
using CyberCafe.Contracts.Common;
using CyberCafe.Contracts.Products;
using CyberCafe.Web.Services;
using CyberCafe.Web.Services.Auth;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging.Abstractions;

namespace CyberCafe.Tests.Web;

public class AuthClientTests
{
    /// <summary>Lưu phiên trong RAM thay cho ProtectedSessionStorage (cần trình duyệt).</summary>
    private sealed class InMemoryTokenStorage : ITokenStorage
    {
        public SessionData? Saved;
        public Task SaveAsync(SessionData data) { this.Saved = data; return Task.CompletedTask; }
        public Task<SessionData?> LoadAsync() => Task.FromResult(this.Saved);
        public Task ClearAsync() { this.Saved = null; return Task.CompletedTask; }
    }

    // JWT giả: chỉ cần payload đọc được (Web không kiểm chữ ký — đó là việc của Api)
    private static string FakeJwt(string name, string role)
    {
        static string B64(string json) => Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{B64("{\"alg\":\"HS256\"}")}.{B64($"{{\"sub\":\"7\",\"name\":\"{name}\",\"role\":\"{role}\"}}")}.chu-ky-gia";
    }

    private static AuthResponse Tokens(string access, string refresh, double accessMinutes = 15) => new(
        access, DateTime.UtcNow.AddMinutes(accessMinutes), refresh, DateTime.UtcNow.AddDays(7),
        7, "an@cybercafe.local", "An", "0901234567", Roles.Customer);

    // Dựng AuthSession với "Api auth" giả (chỉ dùng cho /api/auth/refresh)
    private static (AuthSession Session, FakeHttpHandler AuthApi) CreateSession(Func<HttpRequestMessage, HttpResponseMessage>? refreshResponder = null)
    {
        FakeHttpHandler authApi = new(refreshResponder ?? (_ => FakeHttpHandler.Json(Tokens("access-2", "refresh-2"))));
        AuthSession session = new(new InMemoryTokenStorage(), new AuthApiClient(authApi.CreateClient()),
            TimeProvider.System, NullLogger<AuthSession>.Instance);
        return (session, authApi);
    }

    // Dựng MenuApiClient đi qua BearerTokenHandler → FakeHttpHandler (giống đường ống thật của IHttpClientFactory)
    private static MenuApiClient CreateMenuClient(FakeHttpHandler api, AuthSession? session)
    {
        BearerTokenHandler bearer = new() { InnerHandler = api };
        return new MenuApiClient(new HttpClient(bearer) { BaseAddress = new Uri("http://api.test/") }, session);
    }

    private static HttpResponseMessage EmptyPage() => FakeHttpHandler.Json(new PagedResult<ProductDto>([], 1, 20, 0));

    // Kiểm tra: đã đăng nhập → handler gắn Bearer + X-Correlation-Id; chưa đăng nhập → không có Bearer.
    [Fact]
    public async Task Handler_AttachesBearer_OnlyWhenSignedIn()
    {
        (AuthSession session, _) = CreateSession();
        FakeHttpHandler api = new(_ => EmptyPage());
        MenuApiClient client = CreateMenuClient(api, session);

        await client.GetPageAsync(new ProductQuery());           // chưa đăng nhập
        await session.SignInAsync(Tokens("access-1", "refresh-1"));
        await client.GetPageAsync(new ProductQuery());           // đã đăng nhập

        Assert.Null(api.Requests[0].Bearer);
        Assert.Equal("access-1", api.Requests[1].Bearer);
    }

    // Kiểm tra: Api trả 401 → handler refresh 1 lần → gửi lại với token mới → thành công.
    [Fact]
    public async Task Handler_On401_RefreshesOnce_AndRetries()
    {
        (AuthSession session, FakeHttpHandler authApi) = CreateSession();
        await session.SignInAsync(Tokens("access-1", "refresh-1"));
        FakeHttpHandler api = new(r => r.Headers.Authorization?.Parameter == "access-2"
            ? EmptyPage()
            : FakeHttpHandler.Problem(HttpStatusCode.Unauthorized, "Unauthorized"));

        await CreateMenuClient(api, session).GetPageAsync(new ProductQuery());

        string?[] expected = ["access-1", "access-2"];
        Assert.Equal(expected, api.Requests.Select(r => r.Bearer));
        Assert.Equal("/api/auth/refresh", Assert.Single(authApi.Requests).PathAndQuery);
        Assert.Contains("refresh-1", authApi.Requests[0].Body); // gửi đúng refresh token cũ
    }

    // Kiểm tra: refresh cũng bị từ chối (vé hết hạn/thu hồi) → đăng xuất phiên, trang nhận 401 (ApiException).
    [Fact]
    public async Task Handler_RefreshRejected_SignsOut()
    {
        (AuthSession session, _) = CreateSession(_ => FakeHttpHandler.Problem(HttpStatusCode.Unauthorized, "Refresh token đã bị thu hồi"));
        await session.SignInAsync(Tokens("access-1", "refresh-1"));
        FakeHttpHandler api = new(_ => FakeHttpHandler.Problem(HttpStatusCode.Unauthorized, "Unauthorized"));

        ApiException ex = await Assert.ThrowsAsync<ApiException>(() => CreateMenuClient(api, session).GetPageAsync(new ProductQuery()));

        Assert.Equal(HttpStatusCode.Unauthorized, ex.Status);
        Assert.Null(await session.GetAsync());
    }

    // Kiểm tra: nhiều request cùng gặp 401 với CÙNG token → chỉ refresh 1 lần (tránh Api coi là dùng lại vé).
    [Fact]
    public async Task Session_ConcurrentRefresh_CallsApiOnce()
    {
        (AuthSession session, FakeHttpHandler authApi) = CreateSession();
        await session.SignInAsync(Tokens("access-1", "refresh-1"));

        bool[] results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => session.RefreshAsync("access-1")));

        Assert.All(results, Assert.True);
        Assert.Single(authApi.Requests);
        Assert.Equal("access-2", await session.GetAccessTokenAsync());
    }

    // Kiểm tra: access token sắp hết hạn → GetAccessTokenAsync tự refresh TRƯỚC khi gửi (không đợi 401).
    [Fact]
    public async Task Session_ExpiringAccessToken_RefreshesProactively()
    {
        (AuthSession session, FakeHttpHandler authApi) = CreateSession();
        await session.SignInAsync(Tokens("access-1", "refresh-1", accessMinutes: 0.1)); // còn 6 giây

        Assert.Equal("access-2", await session.GetAccessTokenAsync());
        Assert.Single(authApi.Requests);
    }

    // Kiểm tra: provider đọc tên + role từ JWT; đăng nhập/đăng xuất bắn AuthenticationStateChanged.
    [Fact]
    public async Task StateProvider_ReadsClaims_AndNotifiesOnSignInOut()
    {
        (AuthSession session, _) = CreateSession();
        using JwtAuthenticationStateProvider provider = new(session);
        List<Task<AuthenticationState>> notifications = [];
        provider.AuthenticationStateChanged += task => notifications.Add(task);

        Assert.False((await provider.GetAuthenticationStateAsync()).User.Identity?.IsAuthenticated);

        await session.SignInAsync(Tokens(FakeJwt("Barista Ca Sáng", Roles.Barista), "refresh-1"));
        ClaimsPrincipal user = (await notifications[0]).User;
        await session.SignOutAsync();

        Assert.Equal("Barista Ca Sáng", user.Identity?.Name);
        Assert.True(user.IsInRole(Roles.Barista));
        Assert.False(user.IsInRole(Roles.Admin));
        Assert.False((await notifications[1]).User.Identity?.IsAuthenticated);
    }

    // Kiểm tra: token rác (không đủ 3 phần / payload không phải JSON) → coi như chưa đăng nhập, không ném lỗi.
    [Theory]
    [InlineData("khong-phai-jwt")]
    [InlineData("a.%%%.c")]
    public void ClaimsReader_GarbageToken_ReturnsNull(string token)
    {
        Assert.Null(JwtClaimsReader.ToPrincipal(token));
    }
}
