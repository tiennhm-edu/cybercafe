// ============================================================================
// OrdersEndpointTests.cs — test tích hợp /api/orders (Buổi 32–41 · đặt hàng, luồng trạng thái, Include).
// Mỗi test 1 factory riêng → database InMemory riêng, Id đơn luôn bắt đầu từ 1.
// Buổi 42–47: khách (customer) đặt đơn, barista đổi trạng thái — quyền chi tiết xem AuthorizationTests.
// Buổi 49–53: cùng các test này chạy qua aggregate + dispatcher + read model — hợp đồng HTTP không đổi.
// ============================================================================
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CyberCafe.Contracts.Common;
using CyberCafe.Contracts.Orders;
using CyberCafe.Domain.Orders;
using CyberCafe.Domain.Payments;
using CyberCafe.Domain.Products;

namespace CyberCafe.Api.Tests;

public class OrdersEndpointTests : IAsyncLifetime
{
    private readonly CyberCafeApiFactory _factory = new();
    private HttpClient _client = default!;   // khách hàng (Customer) seed sẵn
    private HttpClient _barista = default!;  // nhân viên quầy

    public async Task InitializeAsync()
    {
        this._client = await this._factory.CustomerAsync();
        this._barista = await this._factory.BaristaAsync();
    }

    public async Task DisposeAsync() => await this._factory.DisposeAsync();

    private Task<HttpResponseMessage> PutStatusAsync(int id, string status) =>
        // Gửi JSON "thô" để thử cả giá trị enum không hợp lệ. Đổi trạng thái là việc của barista.
        this._barista.PutAsync($"/api/orders/{id}/status", JsonContent.Create(new { status }));

    // Kiểm tra: đặt hàng → 201, server tự tính tiền theo size + giảm giá, lưu đúng dòng, báo barista đúng 1 lần.
    [Fact]
    public async Task Place_Valid_Returns201_ComputesTotals_AndNotifiesBaristas()
    {
        HttpResponseMessage response = await this._client.PostAsJsonAsync("/api/orders", TestData.Order("giam20k"), CyberCafeApiFactory.Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        OrderDto order = await response.ReadAsync<OrderDto>();
        Assert.Equal("CC-0001", order.Code);
        Assert.Equal(103000, order.TotalAmount);   // 2 × 34.000 (size M) + 35.000 (bánh bỏ qua size L)
        Assert.Equal(20000, order.DiscountAmount);  // voucher GIAM20K
        Assert.Equal(83000, order.FinalAmount);
        Assert.Equal(8, order.LoyaltyPoints);       // 83.000 / 10.000
        Assert.Equal(DrinkSize.S, order.Items.Single(i => i.ProductId == 7).Size);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(order.Id, Assert.Single(this._factory.Notifier.Placed).Id);
    }

    // Kiểm tra: GET /api/orders/{id} đọc lại từ DB đủ dòng + món + giảm giá + thanh toán (Include/ThenInclude).
    [Fact]
    public async Task GetById_LoadsItemsDiscountAndPayment()
    {
        PlaceOrderRequest request = TestData.Order("MEMBER10");
        request.PaymentMethod = PaymentMethod.Card;
        request.CardNumber = "4111111111111234";
        OrderDto placed = await this._client.PlaceAsync(request);

        OrderDto order = (await this._client.GetFromJsonAsync<OrderDto>($"/api/orders/{placed.Id}", CyberCafeApiFactory.Json))!;

        Assert.Equal(2, order.Items.Count);
        Assert.Equal("Cà phê sữa đá", order.Items[0].ProductName);
        Assert.Contains("10%", order.DiscountText);
        Assert.Equal(PaymentMethod.Card, order.PaymentMethod);
        Assert.Contains("****1234", order.PaymentText); // chỉ lưu 4 số cuối
    }

    // Kiểm tra: dữ liệu sai → 400 (món không tồn tại, mã giảm giá sai, SĐT sai, giỏ rỗng, thẻ thiếu số).
    [Theory]
    [InlineData("unknown-product")]
    [InlineData("bad-code")]
    [InlineData("bad-phone")]
    [InlineData("empty")]
    [InlineData("card-without-number")]
    public async Task Place_InvalidInput_Returns400(string scenario)
    {
        PlaceOrderRequest request = TestData.Order();
        switch (scenario)
        {
            case "unknown-product": request.Items = [new OrderLineRequest(999, DrinkSize.S, 1)]; break;
            case "bad-code": request.DiscountCode = "FREE100"; break;
            case "bad-phone": request.PhoneNumber = "12345"; break;
            case "empty": request.Items = []; break;
            case "card-without-number": request.PaymentMethod = PaymentMethod.Card; break;
        }

        HttpResponseMessage response = await this._client.PostAsJsonAsync("/api/orders", request, CyberCafeApiFactory.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Kiểm tra: đặt món đang tạm hết (Matcha, id 6) → 409 Conflict do domain từ chối.
    [Fact]
    public async Task Place_UnavailableProduct_Returns409()
    {
        HttpResponseMessage response = await this._client.PostAsJsonAsync("/api/orders",
            TestData.Order(null, new OrderLineRequest(6, DrinkSize.S, 1)), CyberCafeApiFactory.Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // Kiểm tra: luồng hợp lệ Pending → Preparing → Ready → Completed, mỗi bước báo realtime.
    [Fact]
    public async Task ChangeStatus_ValidFlow_ReachesCompleted()
    {
        OrderDto order = await this._client.PlaceAsync();

        foreach (string status in new[] { "Preparing", "Ready", "Completed" })
        {
            HttpResponseMessage response = await this.PutStatusAsync(order.Id, status);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        OrderDto final = (await this._client.GetFromJsonAsync<OrderDto>($"/api/orders/{order.Id}", CyberCafeApiFactory.Json))!;
        Assert.Equal(OrderStatus.Completed, final.Status);
        Assert.Equal(3, this._factory.Notifier.StatusChanged.Count);
    }

    // Kiểm tra: nhảy cóc Pending → Ready → 409, trạng thái giữ nguyên, không báo realtime.
    [Fact]
    public async Task ChangeStatus_SkippingStep_Returns409()
    {
        OrderDto order = await this._client.PlaceAsync();

        HttpResponseMessage response = await this.PutStatusAsync(order.Id, "Ready");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Empty(this._factory.Notifier.StatusChanged);
    }

    // Kiểm tra: giá trị enum lạ hoặc thiếu status → 400 (lỗi định dạng, khác 409 lỗi nghiệp vụ).
    [Fact]
    public async Task ChangeStatus_UnknownOrMissingStatus_Returns400()
    {
        OrderDto order = await this._client.PlaceAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await this.PutStatusAsync(order.Id, "Flying")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await this._barista.PutAsync($"/api/orders/{order.Id}/status", JsonContent.Create(new { }))).StatusCode);
    }

    // Kiểm tra: đơn không tồn tại → 404 cho cả GET, đổi trạng thái và hủy.
    [Fact]
    public async Task UnknownOrder_Returns404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await this._client.GetAsync("/api/orders/42")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await this.PutStatusAsync(42, "Preparing")).StatusCode);   // barista
        Assert.Equal(HttpStatusCode.NotFound, (await this._client.PostAsync("/api/orders/42/cancel", null)).StatusCode);
    }

    // Kiểm tra: NHÂN VIÊN hủy được khi đang pha; đơn đã Ready thì không hủy được (409).
    [Fact]
    public async Task Cancel_AllowedWhilePreparing_RejectedWhenReady()
    {
        OrderDto first = await this._client.PlaceAsync();
        OrderDto second = await this._client.PlaceAsync();
        await this.PutStatusAsync(first.Id, "Preparing");
        await this.PutStatusAsync(second.Id, "Preparing");
        await this.PutStatusAsync(second.Id, "Ready");

        HttpResponseMessage cancelFirst = await this._barista.PostAsync($"/api/orders/{first.Id}/cancel", null);
        HttpResponseMessage cancelSecond = await this._barista.PostAsync($"/api/orders/{second.Id}/cancel", null);

        Assert.Equal(OrderStatus.Cancelled, (await cancelFirst.ReadAsync<OrderDto>()).Status);
        Assert.Equal(HttpStatusCode.Conflict, cancelSecond.StatusCode);
    }

    // Kiểm tra (b53 · CQRS): READ MODEL (projection) ra ĐÚNG dữ liệu như phía GHI (aggregate → DTO) — cùng 1 đơn
    // đọc qua POST (kết quả command), GET /{id}, GET /mine và bảng quầy phải giống hệt nhau từng trường.
    [Fact]
    public async Task ReadModel_MatchesWriteModel()
    {
        PlaceOrderRequest request = TestData.Order("MEMBER10", new OrderLineRequest(1, DrinkSize.L, 2), new OrderLineRequest(7, DrinkSize.M, 1), new OrderLineRequest(4, DrinkSize.S, 1));
        request.PaymentMethod = PaymentMethod.Momo;
        OrderDto written = await this._client.PlaceAsync(request);

        OrderDto byId = (await this._client.GetFromJsonAsync<OrderDto>($"/api/orders/{written.Id}", CyberCafeApiFactory.Json))!;
        OrderDto mine = Assert.Single((await this._client.GetFromJsonAsync<PagedResult<OrderDto>>("/api/orders/mine", CyberCafeApiFactory.Json))!.Items);
        OrderDto board = Assert.Single((await this._barista.GetFromJsonAsync<PagedResult<OrderDto>>("/api/orders", CyberCafeApiFactory.Json))!.Items);

        // record có List bên trong so sánh theo tham chiếu → so bằng JSON (từng trường, kể cả Items)
        string expected = JsonSerializer.Serialize(written, CyberCafeApiFactory.Json);
        Assert.Equal(expected, JsonSerializer.Serialize(byId, CyberCafeApiFactory.Json));
        Assert.Equal(expected, JsonSerializer.Serialize(mine, CyberCafeApiFactory.Json));
        Assert.Equal(expected, JsonSerializer.Serialize(board, CyberCafeApiFactory.Json));
    }

    // Kiểm tra (b50 · domain event): 1 lần đặt + 2 lần đổi trạng thái → đúng 1 OrderPlaced và 2 OrderStatusChanged
    // được phát SAU khi lưu, mang dữ liệu đã lưu (Id thật, trạng thái mới).
    [Fact]
    public async Task DomainEvents_ArePublishedOncePerChange_AfterSave()
    {
        OrderDto order = await this._client.PlaceAsync();

        await this.PutStatusAsync(order.Id, "Preparing");
        await this.PutStatusAsync(order.Id, "Ready");

        Assert.Equal(order.Id, Assert.Single(this._factory.Notifier.Placed).Id);
        Assert.Equal([OrderStatus.Preparing, OrderStatus.Ready], this._factory.Notifier.StatusChanged.Select(o => o.Status).ToArray());
    }

    // Kiểm tra: danh sách mặc định chỉ gồm đơn đang chạy (đơn đã hủy không hiện ở quầy barista).
    [Fact]
    public async Task List_Default_ReturnsOnlyActiveOrders()
    {
        OrderDto active = await this._client.PlaceAsync();
        OrderDto cancelled = await this._client.PlaceAsync();
        await this._client.PostAsync($"/api/orders/{cancelled.Id}/cancel", null);

        PagedResult<OrderDto> page = (await this._barista.GetFromJsonAsync<PagedResult<OrderDto>>("/api/orders", CyberCafeApiFactory.Json))!;
        PagedResult<OrderDto> onlyCancelled = (await this._barista.GetFromJsonAsync<PagedResult<OrderDto>>("/api/orders?status=Cancelled", CyberCafeApiFactory.Json))!;

        Assert.Equal(active.Id, Assert.Single(page.Items).Id);
        Assert.Equal(cancelled.Id, Assert.Single(onlyCancelled.Items).Id);
    }
}
