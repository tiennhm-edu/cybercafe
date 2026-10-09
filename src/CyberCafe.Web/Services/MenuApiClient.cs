// ============================================================================
// MenuApiClient.cs — typed HttpClient cho /api/products (Buổi 32–35 · thay MenuService in-memory).
// Typed client: Program.cs đăng ký AddHttpClient<MenuApiClient>(...) → IHttpClientFactory tạo
// HttpClient (đã gắn BaseAddress) và tái sử dụng kết nối. Trang chỉ @inject MenuApiClient.
// ⚠️ Lỗi hay gặp: tự "new HttpClient()" trong mỗi component → cạn socket (socket exhaustion) khi tải cao.
// Buổi 42–47: nhận thêm AuthSession → thêm/sửa/xóa món tự kèm token Admin (qua BearerTokenHandler).
// ============================================================================
using System.Net;
using CyberCafe.Contracts.Common;
using CyberCafe.Contracts.Products;
using CyberCafe.Domain.Products;
using CyberCafe.Web.Services.Auth;

namespace CyberCafe.Web.Services;

/// <summary>Đọc / ghi thực đơn qua CyberCafe.Api.</summary>
public class MenuApiClient(HttpClient http, AuthSession? session = null) : ApiClientBase(http, session)
{
    // 👉 Bước 10 (b40.md)
    /// <summary>
    /// Toàn bộ thực đơn dưới dạng domain Product (cho trang Menu, ProductCard, CartState).
    /// Lấy 1 trang lớn nhất (100 món) — quán nhỏ, đủ dùng; menu lớn hơn thì phân trang trên UI.
    /// </summary>
    public async Task<IReadOnlyList<Product>> GetMenuAsync(CancellationToken ct = default)
    {
        PagedResult<ProductDto> page = await this.GetPageAsync(new ProductQuery { PageSize = ProductQuery.MaxPageSize }, ct);
        return page.Items.Select(dto => dto.ToDomain()).ToList();
    }

    /// <summary>1 trang thực đơn theo bộ lọc (trang quản trị).</summary>
    public Task<PagedResult<ProductDto>> GetPageAsync(ProductQuery query, CancellationToken ct = default) =>
        // Đường dẫn tương đối KHÔNG có "/" đầu → ghép đúng với BaseAddress "http://localhost:5180/"
        this.SendAsync<PagedResult<ProductDto>>(new HttpRequestMessage(HttpMethod.Get, $"api/products?{query.ToQueryString()}"), ct);

    /// <summary>Chi tiết 1 món; không có → null (404 là kết quả hợp lệ, không phải lỗi).</summary>
    public async Task<ProductDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        try
        {
            return await this.SendAsync<ProductDto>(new HttpRequestMessage(HttpMethod.Get, $"api/products/{id}"), ct);
        }
        catch (ApiException ex) when (ex.Status == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <summary>POST — thêm món, trả về món đã có Id.</summary>
    public Task<ProductDto> CreateAsync(ProductRequest request, CancellationToken ct = default) =>
        this.SendAsync<ProductDto>(JsonRequest(HttpMethod.Post, "api/products", request), ct);

    /// <summary>PUT — sửa món.</summary>
    public Task<ProductDto> UpdateAsync(int id, ProductRequest request, CancellationToken ct = default) =>
        this.SendAsync<ProductDto>(JsonRequest(HttpMethod.Put, $"api/products/{id}", request), ct);

    /// <summary>DELETE — xóa món (409 nếu món đã có trong đơn).</summary>
    public Task DeleteAsync(int id, CancellationToken ct = default) =>
        this.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"api/products/{id}"), ct);
}
