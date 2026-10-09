// ============================================================================
// ApiClientTests.cs — unit test MenuApiClient / OrderApiClient (Buổi 32–35 · typed HttpClient).
// Kiểm tra: dựng đúng URL/method/body, đổi DTO → domain, đổi lỗi HTTP/mạng → ApiException.
// ============================================================================
using System.Net;
using CyberCafe.Contracts.Common;
using CyberCafe.Contracts.Orders;
using CyberCafe.Contracts.Products;
using CyberCafe.Domain.Orders;
using CyberCafe.Domain.Payments;
using CyberCafe.Domain.Products;
using CyberCafe.Web.Services;

namespace CyberCafe.Tests.Web;

public class ApiClientTests
{
    private static readonly ProductDto Latte = new(3, ProductTypes.Coffee, "Americano", 35000, "Arabica", "Espresso pha loãng", "☕", true);
    private static readonly ProductDto Croissant = new(8, ProductTypes.Cake, "Croissant bơ", 25000, "Bơ", "", "🥐", false);

    private static OrderDto SampleOrder(OrderStatus status = OrderStatus.Pending) => new(
        7, "CC-0007", status, DateTime.Now, "An", "0901234567", 3, null,
        [new OrderItemDto(3, "Americano", "☕", true, DrinkSize.M, 1, 40000, 40000)],
        40000, null, 0, 40000, PaymentMethod.Cash, "Tiền mặt");

    // Kiểm tra: GetMenuAsync gọi đúng URL và đổi DTO thành đúng class domain (Coffee/Cake) để ProductCard dùng lại.
    [Fact]
    public async Task GetMenu_MapsDtosToDomainTypes()
    {
        FakeHttpHandler handler = new(_ => FakeHttpHandler.Json(new PagedResult<ProductDto>([Latte, Croissant], 1, 100, 2)));
        MenuApiClient client = new(handler.CreateClient());

        IReadOnlyList<Product> menu = await client.GetMenuAsync();

        Assert.Equal("/api/products?page=1&pageSize=100&sortBy=id", handler.Requests.Single().PathAndQuery);
        Coffee coffee = Assert.IsType<Coffee>(menu[0]);
        Assert.Equal("Arabica", coffee.BeanType);
        Assert.Equal(40000, coffee.GetPrice(DrinkSize.M)); // đa hình vẫn hoạt động sau khi map
        Assert.False(Assert.IsType<Cake>(menu[1]).IsAvailable);
    }

    // Kiểm tra: query string escape từ khóa có dấu/khoảng trắng và đủ tham số lọc.
    [Fact]
    public async Task GetPage_BuildsEscapedQueryString()
    {
        FakeHttpHandler handler = new(_ => FakeHttpHandler.Json(new PagedResult<ProductDto>([], 2, 5, 0)));
        MenuApiClient client = new(handler.CreateClient());

        await client.GetPageAsync(new ProductQuery { Search = "trà đào", Type = ProductTypes.Tea, Page = 2, PageSize = 5, SortBy = "price", Desc = true });

        Assert.Equal("/api/products?page=2&pageSize=5&sortBy=price&desc=true&type=tea&search=tr%C3%A0%20%C4%91%C3%A0o",
            handler.Requests.Single().PathAndQuery);
    }

    // Kiểm tra: GET 404 → null (không phải exception).
    [Fact]
    public async Task GetById_NotFound_ReturnsNull()
    {
        FakeHttpHandler handler = new(_ => FakeHttpHandler.Problem(HttpStatusCode.NotFound, "Not Found"));

        Assert.Null(await new MenuApiClient(handler.CreateClient()).GetByIdAsync(99));
    }

    // Kiểm tra: 400 ValidationProblem → ApiException chứa thông báo lỗi của từng field.
    [Fact]
    public async Task Create_ValidationProblem_ThrowsApiExceptionWithFieldErrors()
    {
        FakeHttpHandler handler = new(_ => FakeHttpHandler.Problem(HttpStatusCode.BadRequest, "One or more validation errors occurred.",
            errors: new() { ["Name"] = ["Tên món từ 2 đến 100 ký tự"] }));
        MenuApiClient client = new(handler.CreateClient());

        ApiException ex = await Assert.ThrowsAsync<ApiException>(() => client.CreateAsync(new ProductRequest { Name = "A", Variant = "x" }));

        Assert.Equal(HttpStatusCode.BadRequest, ex.Status);
        Assert.Contains("Tên món từ 2 đến 100 ký tự", ex.Message);
        Assert.Equal(HttpMethod.Post, handler.Requests.Single().Method);
    }

    // Kiểm tra: API không chạy (lỗi mạng) → ApiException 503 với hướng dẫn tiếng Việt, không lộ HttpRequestException.
    [Fact]
    public async Task NetworkFailure_BecomesServiceUnavailable()
    {
        FakeHttpHandler handler = new(_ => throw new HttpRequestException("Connection refused"));

        ApiException ex = await Assert.ThrowsAsync<ApiException>(() => new MenuApiClient(handler.CreateClient()).GetMenuAsync());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, ex.Status);
        Assert.Contains("CyberCafe.Api", ex.Message);
    }

    // Kiểm tra: đặt hàng gửi POST /api/orders với enum dạng chuỗi và đọc lại OrderDto.
    [Fact]
    public async Task PlaceOrder_PostsJson_AndReadsOrder()
    {
        FakeHttpHandler handler = new(_ => FakeHttpHandler.Json(SampleOrder(), HttpStatusCode.Created));
        OrderApiClient client = new(handler.CreateClient());

        OrderDto order = await client.PlaceOrderAsync(new PlaceOrderRequest
        {
            CustomerName = "An",
            PhoneNumber = "0901234567",
            Items = [new OrderLineRequest(3, DrinkSize.M, 1)],
            PaymentMethod = PaymentMethod.Momo,
        });

        var request = handler.Requests.Single();
        Assert.Equal("/api/orders", request.PathAndQuery);
        Assert.Contains("\"paymentMethod\":\"Momo\"", request.Body);
        Assert.Contains("\"size\":\"M\"", request.Body);
        Assert.Equal("CC-0007", order.Code);
    }

    // Kiểm tra: đổi trạng thái gửi PUT đúng URL; 409 từ API → ApiException mang thông báo "detail".
    [Fact]
    public async Task ChangeStatus_Conflict_ThrowsWithDetail()
    {
        FakeHttpHandler handler = new(_ => FakeHttpHandler.Problem(HttpStatusCode.Conflict, "Không đổi được trạng thái",
            "Không thể chuyển đơn CC-0007 từ Chờ pha chế sang Sẵn sàng"));
        OrderApiClient client = new(handler.CreateClient());

        ApiException ex = await Assert.ThrowsAsync<ApiException>(() => client.ChangeStatusAsync(7, OrderStatus.Ready));

        Assert.Equal(HttpStatusCode.Conflict, ex.Status);
        Assert.StartsWith("Không thể chuyển đơn", ex.Message);
        Assert.Equal((HttpMethod.Put, "/api/orders/7/status"), (handler.Requests[0].Method, handler.Requests[0].PathAndQuery));
        Assert.Contains("\"status\":\"Ready\"", handler.Requests[0].Body);
    }
}
