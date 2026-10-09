// ============================================================================
// AuthorizationTests.cs — 401/403 theo vai trò + IDOR trên REST và hub (Buổi 42–47).
// 👉 Bước 15 (b47.md): 401 = "bạn là ai?" (chưa đăng nhập / token sai); 403 = "biết bạn là ai, nhưng không được làm việc này".
// ============================================================================
using System.Net;
using System.Net.Http.Json;
using CyberCafe.Api.Auth;
using CyberCafe.Contracts.Common;
using CyberCafe.Contracts.Orders;
using CyberCafe.Contracts.Products;
using CyberCafe.Contracts.Realtime;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

namespace CyberCafe.Api.Tests;

public class AuthorizationTests(CyberCafeApiFactory factory) : IClassFixture<CyberCafeApiFactory>
{
    private static readonly ProductRequest NewCake = new() { Type = ProductTypes.Cake, Name = "Bánh bông lan", Price = 15000, Variant = "Trứng muối" };

    private async Task<HttpClient> ClientForAsync(string role) => role switch
    {
        "admin" => await factory.AdminAsync(),
        "barista" => await factory.BaristaAsync(),
        "customer" => await factory.CustomerAsync(),
        _ => factory.CreateClient(), // ẩn danh
    };

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string method, string url) => method switch
    {
        "POST-product" => client.PostAsJsonAsync(url, NewCake, CyberCafeApiFactory.Json),
        "POST-order" => client.PostAsJsonAsync(url, TestData.Order(), CyberCafeApiFactory.Json),
        "PUT-status" => client.PutAsync(url, JsonContent.Create(new { status = "Preparing" })),
        _ => client.GetAsync(url),
    };

    // Kiểm tra: bảng quyền — mỗi vai trò gọi mỗi endpoint nhận đúng mã 200/201/401/403.
    // (Đơn #1 do khách tạo ở đầu test để PUT status có dữ liệu thật; 403 phải xảy ra TRƯỚC khi đụng dữ liệu.)
    [Theory]
    [InlineData("anonymous", "GET", "/api/products", HttpStatusCode.OK)]
    [InlineData("anonymous", "POST-product", "/api/products", HttpStatusCode.Unauthorized)]
    [InlineData("anonymous", "POST-order", "/api/orders", HttpStatusCode.Unauthorized)]
    [InlineData("anonymous", "GET", "/api/orders", HttpStatusCode.Unauthorized)]
    [InlineData("customer", "POST-product", "/api/products", HttpStatusCode.Forbidden)]
    [InlineData("customer", "GET", "/api/orders", HttpStatusCode.Forbidden)]
    [InlineData("customer", "PUT-status", "/api/orders/1/status", HttpStatusCode.Forbidden)]
    [InlineData("customer", "GET", "/api/reports/daily-revenue", HttpStatusCode.Forbidden)]
    [InlineData("barista", "POST-product", "/api/products", HttpStatusCode.Forbidden)]
    [InlineData("barista", "POST-order", "/api/orders", HttpStatusCode.Forbidden)]
    [InlineData("barista", "GET", "/api/reports/daily-revenue", HttpStatusCode.Forbidden)]
    [InlineData("barista", "GET", "/api/orders", HttpStatusCode.OK)]
    [InlineData("admin", "GET", "/api/reports/daily-revenue", HttpStatusCode.OK)]
    [InlineData("admin", "GET", "/api/orders", HttpStatusCode.OK)]
    public async Task RoleMatrix(string role, string method, string url, HttpStatusCode expected)
    {
        HttpClient client = await this.ClientForAsync(role);

        HttpResponseMessage response = await SendAsync(client, method, url);

        Assert.Equal(expected, response.StatusCode);
    }

    // Kiểm tra: Admin thêm món được (201), barista đổi trạng thái được (200).
    [Fact]
    public async Task Admin_CreatesProduct_Barista_ChangesStatus()
    {
        await using CyberCafeApiFactory own = new();
        OrderDto order = await (await own.CustomerAsync()).PlaceAsync();

        HttpResponseMessage create = await (await own.AdminAsync()).PostAsJsonAsync("/api/products", NewCake, CyberCafeApiFactory.Json);
        HttpResponseMessage status = await (await own.BaristaAsync()).PutAsync($"/api/orders/{order.Id}/status", JsonContent.Create(new { status = "Preparing" }));

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
    }

    // Kiểm tra (IDOR): khách B KHÔNG xem / hủy được đơn của khách A (404 như không tồn tại); "mine" chỉ có đơn của mình.
    [Fact]
    public async Task Idor_CustomerCannotReadOrCancelOthersOrder()
    {
        await using CyberCafeApiFactory own = new();
        HttpClient alice = await own.CustomerAsync();
        HttpClient bob = await own.NewCustomerAsync("bob@cybercafe.local");
        OrderDto aliceOrder = await alice.PlaceAsync();

        HttpResponseMessage bobReads = await bob.GetAsync($"/api/orders/{aliceOrder.Id}");
        HttpResponseMessage bobCancels = await bob.PostAsync($"/api/orders/{aliceOrder.Id}/cancel", null);
        PagedResult<OrderDto> bobMine = (await bob.GetFromJsonAsync<PagedResult<OrderDto>>("/api/orders/mine", CyberCafeApiFactory.Json))!;
        PagedResult<OrderDto> aliceMine = (await alice.GetFromJsonAsync<PagedResult<OrderDto>>("/api/orders/mine", CyberCafeApiFactory.Json))!;
        HttpResponseMessage aliceReads = await alice.GetAsync($"/api/orders/{aliceOrder.Id}");

        Assert.Equal(HttpStatusCode.NotFound, bobReads.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, bobCancels.StatusCode);
        Assert.Empty(bobMine.Items);
        Assert.Equal(aliceOrder.Id, Assert.Single(aliceMine.Items).Id);
        Assert.Equal(HttpStatusCode.OK, aliceReads.StatusCode);
    }

    // Kiểm tra: khách tự hủy được khi còn Pending; barista đã bắt đầu pha thì khách hủy → 409.
    [Fact]
    public async Task Customer_CanCancelOnlyWhilePending()
    {
        await using CyberCafeApiFactory own = new();
        HttpClient customer = await own.CustomerAsync();
        OrderDto pending = await customer.PlaceAsync();
        OrderDto preparing = await customer.PlaceAsync();
        await (await own.BaristaAsync()).PutAsync($"/api/orders/{preparing.Id}/status", JsonContent.Create(new { status = "Preparing" }));

        HttpResponseMessage cancelPending = await customer.PostAsync($"/api/orders/{pending.Id}/cancel", null);
        HttpResponseMessage cancelPreparing = await customer.PostAsync($"/api/orders/{preparing.Id}/cancel", null);

        Assert.Equal(HttpStatusCode.OK, cancelPending.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, cancelPreparing.StatusCode);
    }

    // Kiểm tra (hub): không token → không kết nối được; khách không vào được group barista;
    // khách B không theo dõi được đơn của khách A.
    [Fact]
    public async Task Hub_RequiresToken_RoleForBaristas_AndOwnershipForWatch()
    {
        await using CyberCafeApiFactory own = new();
        HttpClient alice = await own.CustomerAsync();
        OrderDto aliceOrder = await alice.PlaceAsync();
        HttpClient bob = await own.NewCustomerAsync("bob2@cybercafe.local");
        string aliceToken = alice.DefaultRequestHeaders.Authorization!.Parameter!;
        string bobToken = bob.DefaultRequestHeaders.Authorization!.Parameter!;

        await using HubConnection anonymous = Connect(own, null);
        await using HubConnection aliceHub = Connect(own, aliceToken);
        await using HubConnection bobHub = Connect(own, bobToken);

        await Assert.ThrowsAnyAsync<Exception>(() => anonymous.StartAsync()); // negotiate bị 401
        await aliceHub.StartAsync();
        await bobHub.StartAsync();
        await Assert.ThrowsAsync<HubException>(() => aliceHub.InvokeAsync(OrderHubContract.JoinBaristas));
        await Assert.ThrowsAsync<HubException>(() => bobHub.InvokeAsync(OrderHubContract.WatchOrder, aliceOrder.Id));
        await aliceHub.InvokeAsync(OrderHubContract.WatchOrder, aliceOrder.Id); // chủ đơn thì được
    }

    private static HubConnection Connect(CyberCafeApiFactory own, string? token) => new HubConnectionBuilder()
        .WithUrl(new Uri(own.Server.BaseAddress, OrderHubContract.Path.TrimStart('/')), o =>
        {
            o.AccessTokenProvider = () => Task.FromResult(token);
            o.Transports = HttpTransportType.LongPolling;
            o.HttpMessageHandlerFactory = _ => own.Server.CreateHandler();
        })
        .Build();
}
