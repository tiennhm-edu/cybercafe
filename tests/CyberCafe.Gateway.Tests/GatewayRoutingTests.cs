// ============================================================================
// GatewayRoutingTests.cs — API Gateway YARP (Buổi 54): cấu hình route, định tuyến, JWT tập trung.
// WebApplicationFactory chạy NGUYÊN Program.cs của gateway trên TestServer; chỉ thay IForwarderHttpClientFactory
// (HttpClient YARP dùng để chuyển tiếp) bằng RecordingForwarderFactory → biết request đáng lẽ đi tới đâu,
// mang header gì — mà không cần Api / Payment chạy thật.
// 👉 Bước 5 · Bước 6 (b54.md)
// ============================================================================
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;

namespace CyberCafe.Gateway.Tests;

/// <summary>Dựng gateway trong bộ nhớ với khóa JWT giả và bộ chuyển tiếp giả.</summary>
public sealed class GatewayFactory : WebApplicationFactory<GatewayPolicies>
{
    /// <summary>Khóa ký JWT GIẢ chỉ dùng trong test (≥ 32 byte).</summary>
    public const string JwtKey = "test-only-FAKE-gateway-jwt-key-0123456789";

    /// <summary>Các request YARP đã "chuyển tiếp".</summary>
    public ConcurrentQueue<HttpRequestMessage> Forwarded { get; } = new();

    /// <summary>Tạo JWT giống Api phát (issuer, audience, claim "role").</summary>
    public static string Token(string role) => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
    {
        Issuer = "CyberCafe.Api",
        Audience = "CyberCafe.Clients",
        Subject = new ClaimsIdentity([new Claim("sub", "1"), new Claim("name", "Test"), new Claim("role", role)]),
        Expires = DateTime.UtcNow.AddMinutes(5),
        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey)), SecurityAlgorithms.HmacSha256),
    });

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing"); // không nạp appsettings.Development.json (địa chỉ localhost:5180...)
        builder.UseSetting("Jwt:Key", JwtKey);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IForwarderHttpClientFactory>();
            services.AddSingleton<IForwarderHttpClientFactory>(new RecordingForwarderFactory(this.Forwarded));
        });
    }

    // YARP xin HttpMessageInvoker từ factory này để gửi request đi → trả về handler giả trả 200 + echo URL đích
    private sealed class RecordingForwarderFactory(ConcurrentQueue<HttpRequestMessage> log) : IForwarderHttpClientFactory
    {
        public HttpMessageInvoker CreateClient(ForwarderHttpClientContext context) => new(new EchoHandler(log));
    }

    private sealed class EchoHandler(ConcurrentQueue<HttpRequestMessage> log) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            log.Enqueue(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"forwarded:{request.RequestUri}"),
            });
        }
    }
}

public class GatewayRoutingTests(GatewayFactory factory) : IClassFixture<GatewayFactory>
{
    // Kiểm tra: appsettings.json nạp đủ 3 route (api, hubs, payments) và 2 cluster trỏ TÊN service (service discovery),
    // route payments có policy AdminOnly — trùng tên policy đăng ký trong Program.cs.
    [Fact]
    public void Config_LoadsRoutesAndClusters()
    {
        IProxyConfig config = factory.Services.GetRequiredService<IProxyConfigProvider>().GetConfig();

        Assert.Equal(["api", "hubs", "payments"], config.Routes.Select(r => r.RouteId).Order().ToArray());
        Assert.Equal(GatewayPolicies.AdminOnly, config.Routes.Single(r => r.RouteId == "payments").AuthorizationPolicy);
        Assert.Equal("http://api", config.Clusters.Single(c => c.ClusterId == "api").Destinations!.Single().Value.Address);
        Assert.Equal("http://payment", config.Clusters.Single(c => c.ClusterId == "payment").Destinations!.Single().Value.Address);
    }

    // Kiểm tra: /api/* và /hubs/* (ẩn danh) đi tới service "api", GIỮ NGUYÊN đường dẫn + query string; gateway thêm X-Forwarded-*
    // (TestServer không có IP client nên không có X-Forwarded-For — chạy thật thì có; kiểm X-Forwarded-Host thay thế).
    [Theory]
    [InlineData("/api/products?page=2&pageSize=5", "/api/products?page=2&pageSize=5")]
    [InlineData("/hubs/orders/negotiate?negotiateVersion=1", "/hubs/orders/negotiate?negotiateVersion=1")]
    public async Task ApiAndHubRoutes_ForwardToApiService(string path, string expectedPathAndQuery)
    {
        HttpResponseMessage response = await factory.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        HttpRequestMessage forwarded = factory.Forwarded.Last(r => r.RequestUri!.PathAndQuery == expectedPathAndQuery);
        Assert.Equal("api", forwarded.RequestUri!.Host);
        Assert.True(forwarded.Headers.Contains("X-Forwarded-Host"));
    }

    // Kiểm tra: /payments/* — chưa đăng nhập 401, Customer 403 (gateway chặn, KHÔNG chuyển tiếp);
    // Admin → tới service "payment", header Authorization được chuyển nguyên.
    [Fact]
    public async Task PaymentsRoute_IsGuardedByJwtAtGateway()
    {
        HttpClient client = factory.CreateClient();
        int before = factory.Forwarded.Count(r => r.RequestUri!.Host == "payment");

        HttpResponseMessage anonymous = await client.GetAsync("/payments");
        HttpResponseMessage customer = await client.SendAsync(WithToken("/payments", "Customer"));
        HttpResponseMessage admin = await client.SendAsync(WithToken("/payments/orders/7", "Admin"));

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, customer.StatusCode);
        Assert.Equal(HttpStatusCode.OK, admin.StatusCode);
        HttpRequestMessage forwarded = Assert.Single(factory.Forwarded.Where(r => r.RequestUri!.Host == "payment").Skip(before));
        Assert.Equal("/payments/orders/7", forwarded.RequestUri!.AbsolutePath);
        Assert.Equal("Bearer", forwarded.Headers.Authorization?.Scheme);
    }

    // Kiểm tra: đường dẫn không khớp route nào → 404 ngay tại gateway; token giả (sai chữ ký) → 401.
    [Fact]
    public async Task UnknownPath_Is404_AndForgedToken_Is401()
    {
        HttpClient client = factory.CreateClient();
        HttpRequestMessage forged = new(HttpMethod.Get, "/payments");
        forged.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GatewayFactory.Token("Admin")[..^4] + "AAAA");

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/admin/secret")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(forged)).StatusCode);
    }

    private static HttpRequestMessage WithToken(string path, string role)
    {
        HttpRequestMessage request = new(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GatewayFactory.Token(role));
        return request;
    }
}
