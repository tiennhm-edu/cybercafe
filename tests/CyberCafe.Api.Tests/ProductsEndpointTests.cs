// ============================================================================
// ProductsEndpointTests.cs — test tích hợp /api/products (Buổi 32–41 · CRUD, mã trạng thái, LINQ).
// Test ĐỌC dùng chung 1 factory (IClassFixture) vì không đổi dữ liệu;
// test GHI tạo factory riêng (database InMemory riêng) để không ảnh hưởng nhau.
// ============================================================================
using System.Net;
using System.Net.Http.Json;
using CyberCafe.Contracts.Common;
using CyberCafe.Contracts.Products;
using Microsoft.AspNetCore.Mvc;

namespace CyberCafe.Api.Tests;

public class ProductsEndpointTests(CyberCafeApiFactory shared) : IClassFixture<CyberCafeApiFactory>
{
    private readonly HttpClient _client = shared.CreateClient();

    private static ProductRequest NewCake(string name = "Bánh flan") =>
        new() { Type = ProductTypes.Cake, Name = name, Price = 20000, Variant = "Caramen", Emoji = "🍮" };

    // Kiểm tra: GET mặc định trả 8 món seed, sắp theo Id, kèm tổng số.
    [Fact]
    public async Task GetPage_Default_ReturnsSeededMenu()
    {
        PagedResult<ProductDto> page = (await this._client.GetFromJsonAsync<PagedResult<ProductDto>>("/api/products", CyberCafeApiFactory.Json))!;

        Assert.Equal(8, page.TotalCount);
        Assert.Equal(Enumerable.Range(1, 8), page.Items.Select(p => p.Id));
        Assert.Equal("Cà phê", page.Items[0].Category);
    }

    // Kiểm tra: phân trang Skip/Take — trang 2, 3 món/trang → món 4,5,6; tổng 3 trang.
    [Fact]
    public async Task GetPage_Paging_SkipsAndTakes()
    {
        PagedResult<ProductDto> page = (await this._client.GetFromJsonAsync<PagedResult<ProductDto>>("/api/products?page=2&pageSize=3", CyberCafeApiFactory.Json))!;

        Assert.Equal([4, 5, 6], page.Items.Select(p => p.Id).ToArray());
        Assert.Equal(3, page.TotalPages);
        Assert.True(page.HasNext);
    }

    // Kiểm tra: lọc theo loại (TPH discriminator) + sắp giá giảm dần.
    [Fact]
    public async Task GetPage_FilterByTypeAndSortByPriceDesc()
    {
        PagedResult<ProductDto> page = (await this._client.GetFromJsonAsync<PagedResult<ProductDto>>("/api/products?type=tea&sortBy=price&desc=true", CyberCafeApiFactory.Json))!;

        Assert.All(page.Items, p => Assert.Equal(ProductTypes.Tea, p.Type));
        Assert.Equal([45000m, 42000m, 39000m], page.Items.Select(p => p.Price).ToArray());
        Assert.Equal("Matcha", page.Items[0].Variant); // TeaType map vào Variant
    }

    // Kiểm tra: tìm theo tên + chỉ món còn hàng.
    [Fact]
    public async Task GetPage_SearchAndAvailable()
    {
        PagedResult<ProductDto> page = (await this._client.GetFromJsonAsync<PagedResult<ProductDto>>("/api/products?search=Trà&available=true", CyberCafeApiFactory.Json))!;

        Assert.Equal([4, 5], page.Items.Select(p => p.Id).ToArray());
    }

    // Kiểm tra: tham số query sai (pageSize > 100, sortBy lạ) → 400 ValidationProblem.
    [Theory]
    [InlineData("/api/products?pageSize=1000")]
    [InlineData("/api/products?sortBy=hack")]
    [InlineData("/api/products?type=pizza")]
    public async Task GetPage_InvalidQuery_Returns400(string url)
    {
        HttpResponseMessage response = await this._client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Kiểm tra: GET id không tồn tại → 404 dạng ProblemDetails.
    [Fact]
    public async Task GetById_Unknown_Returns404Problem()
    {
        HttpResponseMessage response = await this._client.GetAsync("/api/products/9999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    // Kiểm tra: POST hợp lệ → 201 + Location trỏ tới món mới, GET lại được.
    [Fact]
    public async Task Create_Valid_Returns201WithLocation()
    {
        await using CyberCafeApiFactory factory = new();
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/products", NewCake(), CyberCafeApiFactory.Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        ProductDto created = await response.ReadAsync<ProductDto>();
        Assert.Equal($"/api/products/{created.Id}", response.Headers.Location?.AbsolutePath);
        Assert.Equal("Caramen", (await client.GetFromJsonAsync<ProductDto>(response.Headers.Location, CyberCafeApiFactory.Json))!.Variant);
    }

    // Kiểm tra: POST thiếu tên, giá âm → 400 với lỗi theo từng field.
    [Fact]
    public async Task Create_Invalid_Returns400WithFieldErrors()
    {
        ProductRequest bad = new() { Type = ProductTypes.Coffee, Name = "", Price = -1, Variant = "" };

        HttpResponseMessage response = await this._client.PostAsJsonAsync("/api/products", bad, CyberCafeApiFactory.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        ValidationProblemDetails problem = await response.ReadAsync<ValidationProblemDetails>();
        Assert.Contains(nameof(ProductRequest.Name), problem.Errors.Keys);
        Assert.Contains(nameof(ProductRequest.Price), problem.Errors.Keys);
    }

    // Kiểm tra: PUT đổi tên/giá thành công; đổi loại món (TPH) → 400.
    [Fact]
    public async Task Update_ChangesFields_ButNotType()
    {
        await using CyberCafeApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        ProductRequest request = new() { Type = ProductTypes.Coffee, Name = "Americano đá", Price = 37000, Variant = "Arabica" };

        ProductDto updated = await (await client.PutAsJsonAsync("/api/products/3", request, CyberCafeApiFactory.Json)).ReadAsync<ProductDto>();
        request.Type = ProductTypes.Cake;
        HttpResponseMessage changeType = await client.PutAsJsonAsync("/api/products/3", request, CyberCafeApiFactory.Json);

        Assert.Equal(("Americano đá", 37000m), (updated.Name, updated.Price));
        Assert.Equal(HttpStatusCode.BadRequest, changeType.StatusCode);
    }

    // Kiểm tra: DELETE món chưa bán → 204, GET lại → 404; xóa lần 2 → 404.
    [Fact]
    public async Task Delete_UnsoldProduct_Returns204ThenNotFound()
    {
        await using CyberCafeApiFactory factory = new();
        HttpClient client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/products/8")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/products/8")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync("/api/products/8")).StatusCode);
    }

    // Kiểm tra: DELETE món đã có trong đơn → 409 (giữ lịch sử bán hàng).
    [Fact]
    public async Task Delete_SoldProduct_Returns409()
    {
        await using CyberCafeApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        await client.PlaceAsync(); // đơn có món 1 và 7

        HttpResponseMessage response = await client.DeleteAsync("/api/products/7");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
