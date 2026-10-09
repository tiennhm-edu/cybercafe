// ============================================================================
// AuthTests.cs — đăng ký / đăng nhập / refresh rotation / băm mật khẩu / rate limit (Buổi 42–47).
// ============================================================================
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CyberCafe.Api.Auth;
using CyberCafe.Contracts.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CyberCafe.Api.Tests;

public class AuthTests : IAsyncLifetime
{
    private readonly CyberCafeApiFactory _factory = new();
    private HttpClient _client = default!;

    public Task InitializeAsync()
    {
        this._client = this._factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await this._factory.DisposeAsync();

    private Task<HttpResponseMessage> RefreshAsync(string refreshToken) =>
        this._client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(refreshToken));

    // Kiểm tra: đăng nhập đúng → access token + refresh token + role lấy từ DB.
    [Fact]
    public async Task Login_Valid_ReturnsTokensAndRole()
    {
        AuthResponse auth = await this._client.LoginAsync(DevAccountSeeder.AdminEmail);

        Assert.Equal(Roles.Admin, auth.Role);
        Assert.Equal(3, auth.AccessToken.Split('.').Length); // JWT = header.payload.signature
        Assert.NotEqual(auth.AccessToken, auth.RefreshToken);
        Assert.True(auth.RefreshTokenExpiresAt > auth.AccessTokenExpiresAt);
    }

    // Kiểm tra: sai mật khẩu và email không tồn tại → CÙNG 401 + cùng thông báo (không dò được email).
    [Fact]
    public async Task Login_WrongPasswordOrUnknownEmail_Returns401_WithSameMessage()
    {
        HttpResponseMessage wrongPassword = await this._client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = DevAccountSeeder.AdminEmail, Password = "Sai12345" });
        HttpResponseMessage unknownEmail = await this._client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = "ai.do@cybercafe.local", Password = "Sai12345" });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        Assert.Contains("Sai email hoặc mật khẩu", await wrongPassword.Content.ReadAsStringAsync());
        Assert.Contains("Sai email hoặc mật khẩu", await unknownEmail.Content.ReadAsStringAsync());
    }

    // Kiểm tra: đăng ký luôn ra Customer (client gửi kèm "role": "Admin" cũng bị bỏ qua) và mật khẩu được băm BCrypt.
    [Fact]
    public async Task Register_IgnoresRoleFromClient_AndStoresBCryptHash()
    {
        HttpResponseMessage response = await this._client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "Khach.Moi@CyberCafe.local",
            password = "MatKhau123",
            fullName = "Khách Mới",
            role = "Admin", // cố tình tự phong Admin
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(Roles.Customer, (await response.ReadAsync<AuthResponse>()).Role);
        await this._factory.WithDbAsync(async db =>
        {
            User user = await db.Users.SingleAsync(u => u.Email == "khach.moi@cybercafe.local"); // email lưu chữ thường
            Assert.StartsWith("$2", user.PasswordHash);                      // định dạng BCrypt
            Assert.DoesNotContain("MatKhau123", user.PasswordHash);          // không lưu mật khẩu gốc
            Assert.True(BCrypt.Net.BCrypt.Verify("MatKhau123", user.PasswordHash));
        });
    }

    // Kiểm tra: email trùng (khác hoa/thường) → 409; mật khẩu yếu → 400.
    [Fact]
    public async Task Register_DuplicateEmail409_WeakPassword400()
    {
        HttpResponseMessage duplicate = await this._client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { Email = "CUSTOMER@cybercafe.local", Password = "MatKhau123", FullName = "Trùng" });
        HttpResponseMessage weak = await this._client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { Email = "yeu@cybercafe.local", Password = "12345678", FullName = "Yếu" });

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
    }

    // Kiểm tra: refresh xoay vòng (vé mới khác vé cũ); dùng lại vé cũ → 401 VÀ vé mới cũng bị thu hồi (reuse detection).
    [Fact]
    public async Task Refresh_RotatesToken_AndReuseRevokesWholeFamily()
    {
        AuthResponse first = await this._client.LoginAsync(DevAccountSeeder.CustomerEmail);

        HttpResponseMessage rotated = await this.RefreshAsync(first.RefreshToken);
        AuthResponse second = await rotated.ReadAsync<AuthResponse>();
        HttpResponseMessage reuse = await this.RefreshAsync(first.RefreshToken);       // kẻ trộm dùng lại vé cũ
        HttpResponseMessage afterReuse = await this.RefreshAsync(second.RefreshToken); // vé "hợp lệ" cũng đã bị khóa

        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, afterReuse.StatusCode);
    }

    // Kiểm tra: đăng xuất thu hồi refresh token; DB chỉ lưu HASH của refresh token.
    [Fact]
    public async Task Logout_RevokesRefreshToken_StoredAsHashOnly()
    {
        AuthResponse auth = await this._client.LoginAsync(DevAccountSeeder.CustomerEmail);

        HttpResponseMessage logout = await this._client.PostAsJsonAsync("/api/auth/logout", new RefreshRequest(auth.RefreshToken));
        HttpResponseMessage refresh = await this.RefreshAsync(auth.RefreshToken);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        await this._factory.WithDbAsync(async db =>
            Assert.False(await db.RefreshTokens.AnyAsync(t => t.TokenHash == auth.RefreshToken)));
    }

    // Kiểm tra: /me cần token; có token thì đọc đúng thông tin từ claim.
    [Fact]
    public async Task Me_RequiresToken_AndReadsClaims()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await this._client.GetAsync("/api/auth/me")).StatusCode);

        AuthResponse auth = await this._client.LoginAsync(DevAccountSeeder.BaristaEmail);
        this._client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        MeResponse me = (await this._client.GetFromJsonAsync<MeResponse>("/api/auth/me"))!;

        Assert.Equal((DevAccountSeeder.BaristaEmail, Roles.Barista), (me.Email, me.Role));
    }

    // Kiểm tra: token bị sửa payload (chữ ký không còn khớp) → 401.
    [Fact]
    public async Task TamperedToken_Returns401()
    {
        AuthResponse auth = await this._client.LoginAsync(DevAccountSeeder.CustomerEmail);
        string[] parts = auth.AccessToken.Split('.');
        string fakePayload = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode("{\"sub\":\"1\",\"role\":\"Admin\"}");
        this._client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", $"{parts[0]}.{fakePayload}.{parts[2]}");

        Assert.Equal(HttpStatusCode.Unauthorized, (await this._client.GetAsync("/api/reports/daily-revenue")).StatusCode);
    }

    // Kiểm tra: quá số lần đăng nhập cho phép / phút → 429 kèm Retry-After.
    [Fact]
    public async Task Login_RateLimited_Returns429()
    {
        await using CyberCafeApiFactory limited = new() { LoginPermitLimit = 2 };
        HttpClient client = limited.CreateClient();
        LoginRequest wrong = new() { Email = DevAccountSeeder.AdminEmail, Password = "DoMatKhau1" };

        HttpStatusCode[] codes = new HttpStatusCode[3];
        HttpResponseMessage? last = null;
        for (int i = 0; i < codes.Length; i++)
        {
            last = await client.PostAsJsonAsync("/api/auth/login", wrong);
            codes[i] = last.StatusCode;
        }

        Assert.Equal([HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests], codes);
        Assert.True(last!.Headers.Contains("Retry-After"));
    }

    // Kiểm tra: BCrypt — cùng mật khẩu băm 2 lần ra 2 chuỗi khác nhau (salt), vẫn Verify được.
    [Fact]
    public void PasswordHasher_SaltsEachHash()
    {
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:BCryptWorkFactor"] = "4" })
            .Build();
        BCryptPasswordHasher hasher = new(config);

        string first = hasher.Hash("CafeSua123");
        string second = hasher.Hash("CafeSua123");

        Assert.NotEqual(first, second);
        Assert.True(hasher.Verify("CafeSua123", first));
        Assert.False(hasher.Verify("cafesua123", first));
        Assert.False(hasher.Verify("CafeSua123", "khong-phai-bcrypt"));
    }
}
