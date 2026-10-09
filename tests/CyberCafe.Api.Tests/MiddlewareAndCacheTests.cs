// ============================================================================
// MiddlewareAndCacheTests.cs — correlation id, exception handler, cache thực đơn (Buổi 42–47).
// ============================================================================
using System.Net;
using System.Net.Http.Json;
using CyberCafe.Api.Controllers;
using CyberCafe.Api.Errors;
using CyberCafe.Api.Middleware;
using CyberCafe.Application.Caching;
using CyberCafe.Contracts.Orders;
using CyberCafe.Contracts.Products;
using CyberCafe.Domain.Products;
using CyberCafe.Infrastructure.Caching;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CyberCafe.Api.Tests;

public class MiddlewareAndCacheTests
{
    // Kiểm tra: correlation id client gửi được trả lại nguyên vẹn; không gửi thì server tự tạo.
    [Fact]
    public async Task CorrelationId_EchoedOrGenerated()
    {
        await using CyberCafeApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        HttpRequestMessage withId = new(HttpMethod.Get, "/api/products/1");
        withId.Headers.Add(CorrelationIdMiddleware.HeaderName, "demo-123");

        HttpResponseMessage echoed = await client.SendAsync(withId);
        HttpResponseMessage generated = await client.GetAsync("/api/products/1");

        Assert.Equal("demo-123", echoed.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single());
        Assert.Equal(32, generated.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single().Length);
    }

    // Kiểm tra: lỗi (404 sai URL, 409 nghiệp vụ) đều là ProblemDetails có correlationId để tra log.
    [Fact]
    public async Task Errors_AreProblemDetails_WithCorrelationId()
    {
        await using CyberCafeApiFactory factory = new();
        HttpClient customer = await factory.CustomerAsync();

        HttpResponseMessage unknownUrl = await customer.GetAsync("/api/khong-ton-tai");
        HttpResponseMessage unavailable = await customer.PostAsJsonAsync("/api/orders",
            TestData.Order(null, new OrderLineRequest(6, DrinkSize.S, 1)), CyberCafeApiFactory.Json); // Matcha tạm hết → domain ném InvalidOperationException

        Assert.Equal("application/problem+json", unknownUrl.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.Conflict, unavailable.StatusCode);
        ProblemDetails problem = await unavailable.ReadAsync<ProblemDetails>();
        Assert.Contains("tạm hết", problem.Detail);
        Assert.True(problem.Extensions.ContainsKey("correlationId"));
    }

    // Kiểm tra: handler chỉ "dịch" exception ném từ Domain; exception của thư viện khác để lọt → 500.
    [Fact]
    public async Task DomainExceptionHandler_MapsOnlyDomainExceptions()
    {
        await using CyberCafeApiFactory factory = new();
        using IServiceScope scope = factory.Services.CreateScope();
        DomainExceptionHandler handler = new(scope.ServiceProvider.GetRequiredService<IProblemDetailsService>(), NullLogger<DomainExceptionHandler>.Instance);
        DefaultHttpContext http = new() { RequestServices = scope.ServiceProvider };
        http.Response.Body = new MemoryStream();

        ArgumentException fromDomain = Assert.Throws<ArgumentException>(() => new Coffee("", 1000, "Robusta"));
        bool handledDomain = await handler.TryHandleAsync(http, fromDomain, CancellationToken.None);
        int domainStatus = http.Response.StatusCode;
        bool handledOther = await handler.TryHandleAsync(new DefaultHttpContext(), new InvalidOperationException("lỗi cấu hình EF"), CancellationToken.None);

        Assert.True(handledDomain);
        Assert.Equal(StatusCodes.Status400BadRequest, domainStatus);
        Assert.False(handledOther);
    }

    // Kiểm tra (cache-aside + invalidation): lần 2 lấy từ cache (HIT, không thấy thay đổi "lén" trong DB);
    // admin sửa qua API → filter xóa cache → lần sau MISS và thấy dữ liệu mới.
    [Fact]
    public async Task ProductsCache_HitsUntilAdminWrites()
    {
        await using CyberCafeApiFactory factory = new();
        HttpClient anonymous = factory.CreateClient();
        const string url = "/api/products?type=coffee";

        HttpResponseMessage first = await anonymous.GetAsync(url);
        // Sửa thẳng DB (bỏ qua API) → cache KHÔNG biết → vẫn trả bản cũ
        await factory.WithDbAsync(async db =>
        {
            Product americano = await db.Products.SingleAsync(p => p.Id == 3);
            americano.Name = "Americano (sửa lén)";
            await db.SaveChangesAsync();
        });
        HttpResponseMessage second = await anonymous.GetAsync(url);

        HttpClient admin = await factory.AdminAsync();
        await admin.PutAsJsonAsync("/api/products/3",
            new ProductRequest { Type = ProductTypes.Coffee, Name = "Americano nóng", Price = 35000, Variant = "Arabica" }, CyberCafeApiFactory.Json);
        HttpResponseMessage third = await anonymous.GetAsync(url);

        Assert.Equal("MISS", first.Headers.GetValues(ProductsController.CacheHeader).Single());
        Assert.Equal("HIT", second.Headers.GetValues(ProductsController.CacheHeader).Single());
        Assert.Contains("\"Americano\"", await second.Content.ReadAsStringAsync());
        Assert.Equal("MISS", third.Headers.GetValues(ProductsController.CacheHeader).Single());
        Assert.Contains("Americano nóng", await third.Content.ReadAsStringAsync());
    }

    // Kiểm tra: Redis "chết" (mọi thao tác ném lỗi) → vẫn trả dữ liệu từ DB, không ném exception;
    // sau lỗi đầu tiên tạm bỏ qua cache (không gọi Redis nữa trong 30 giây).
    [Fact]
    public async Task MenuCache_FallsBackToDatabase_WhenCacheFails()
    {
        BrokenCache broken = new();
        MenuCache cache = new(broken, TimeProvider.System, NullLogger<MenuCache>.Instance);
        int loads = 0;

        CacheResult<string> first = await cache.GetOrCreateAsync("k", _ => Task.FromResult($"db-{++loads}"));
        int callsAfterFirst = broken.Calls;
        CacheResult<string> second = await cache.GetOrCreateAsync("k", _ => Task.FromResult($"db-{++loads}"));

        Assert.Equal(("db-1", false), (first.Value, first.Hit));
        Assert.Equal(("db-2", false), (second.Value, second.Hit));
        Assert.Equal(callsAfterFirst, broken.Calls); // lần 2 không chạm vào cache hỏng
    }

    /// <summary>IDistributedCache luôn lỗi — giả lập Redis không kết nối được.</summary>
    private sealed class BrokenCache : IDistributedCache
    {
        public int Calls;

        private Exception Fail()
        {
            this.Calls++;
            return new InvalidOperationException("Redis không kết nối được");
        }

        public byte[]? Get(string key) => throw this.Fail();
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => throw this.Fail();
        public void Refresh(string key) => throw this.Fail();
        public Task RefreshAsync(string key, CancellationToken token = default) => throw this.Fail();
        public void Remove(string key) => throw this.Fail();
        public Task RemoveAsync(string key, CancellationToken token = default) => throw this.Fail();
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw this.Fail();
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => throw this.Fail();
    }
}
