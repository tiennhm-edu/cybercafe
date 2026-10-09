// ============================================================================
// ProductsController.cs — CRUD thực đơn /api/products (Buổi 32–35 · REST CRUD;
//                         Buổi 36–41 · EF Core: AsNoTracking, lọc/sắp xếp/phân trang, projection;
//                         Buổi 42–47 · chỉ Admin được ghi, GET đi qua Redis cache, lỗi domain do handler toàn cục lo;
//                         Buổi 48 · controller MỎNG: HTTP vào → MenuService → HTTP ra;
//                         Buổi 51–53 · GET đi qua query (GetMenuQuery/GetProductByIdQuery), ghi giữ MenuService).
// Bảng mã trạng thái (ghi: thiếu token → 401, không phải Admin → 403):
//   GET    /api/products        200 + PagedResult        (400 nếu query sai)
//   GET    /api/products/{id}   200 | 404
//   POST   /api/products        201 + Location header | 400 ValidationProblem
//   PUT    /api/products/{id}   200 | 400 | 404
//   DELETE /api/products/{id}   204 | 404 | 409 (món đã có trong đơn)
// Buổi 48: LINQ/EF chuyển sang ProductRepository (Infrastructure), luật "không đổi loại", "món đã bán"
//   chuyển sang MenuService (Application). Còn lại ở đây: route, policy, mã HTTP, header X-Cache.
// ============================================================================
using CyberCafe.Api.Auth;
using CyberCafe.Api.Filters;
using CyberCafe.Application.Caching;
using CyberCafe.Application.Common.Messaging;
using CyberCafe.Application.Products;
using CyberCafe.Application.Products.Queries;
using CyberCafe.Contracts.Common;
using CyberCafe.Contracts.Products;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CyberCafe.Api.Controllers;

/// <summary>Quản lý thực đơn.</summary>
// [ApiController]: tự validate DataAnnotations → 400 ValidationProblem; tự đọc body JSON cho tham số kiểu class.
[ApiController]
[Route("api/products")]
// Buổi 42–47: mặc định MỌI action cần policy ManageMenu (Admin); action đọc mở lại bằng [AllowAnonymous].
// "Đóng mặc định, mở có chủ đích" an toàn hơn "mở mặc định, nhớ đóng từng action".
[Authorize(Policy = Policies.ManageMenu)]
public class ProductsController(ISender sender, MenuService menu) : ControllerBase
{
    /// <summary>Tên header cho biết response lấy từ cache (HIT) hay database (MISS) — tiện quan sát khi học.</summary>
    public const string CacheHeader = "X-Cache";

    /// <summary>Danh sách món có tìm kiếm, lọc theo loại / còn hàng, sắp xếp và phân trang.</summary>
    /// <remarks>Ví dụ: GET /api/products?type=tea&amp;sortBy=price&amp;desc=true&amp;page=1&amp;pageSize=5</remarks>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<PagedResult<ProductDto>>(StatusCodes.Status200OK)]
    public async Task<PagedResult<ProductDto>> GetPage([FromQuery] ProductQuery query, CancellationToken ct)
    {
        // 👉 Bước 8 (b47.md): cache-aside — b48 nằm trong MenuService, 👉 Bước 3 (b53.md): giờ là GetMenuQuery
        CacheResult<PagedResult<ProductDto>> result = await sender.Send(new GetMenuQuery(query), ct);
        this.Response.Headers[CacheHeader] = result.Hit ? "HIT" : "MISS"; // header là chuyện HTTP → ở lại controller
        return result.Value;
    }

    /// <summary>Chi tiết 1 món.</summary>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType<ProductDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> GetById(int id, CancellationToken ct)
    {
        CacheResult<ProductDto?> result = await sender.Send(new GetProductByIdQuery(id), ct);
        this.Response.Headers[CacheHeader] = result.Hit ? "HIT" : "MISS";

        // NotFound() trong [ApiController] tự trả body ProblemDetails (type, title, status, traceId)
        return result.Value is null ? this.NotFound() : result.Value;
    }

    /// <summary>Thêm món mới (Admin).</summary>
    [HttpPost]
    [InvalidateMenuCache] // ghi thành công → filter đổi version cache thực đơn
    [ProducesResponseType<ProductDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ProductDto>> Create(ProductRequest request, CancellationToken ct)
    {
        ProductDto created = await menu.CreateAsync(request, ct); // ArgumentException từ domain → 400 (handler)

        // 201 Created + header Location: /api/products/{id} (client biết URL của tài nguyên vừa tạo)
        // ⚠️ Lỗi hay gặp: nameof(GetByIdAsync) — ASP.NET Core bỏ hậu tố "Async" khỏi tên action → không tìm thấy route.
        return this.CreatedAtAction(nameof(this.GetById), new { id = created.Id }, created);
    }

    /// <summary>Sửa món, không đổi được loại món (Admin).</summary>
    [HttpPut("{id:int}")]
    [InvalidateMenuCache]
    [ProducesResponseType<ProductDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ProductDto> Update(int id, ProductRequest request, CancellationToken ct) =>
        // Không có món → NotFoundException (404); đổi loại → ValidationException (400) — xem MenuService
        menu.UpdateAsync(id, request, ct);

    /// <summary>Xóa món chưa từng được bán (Admin). Món đã có trong đơn → 409, hãy đánh dấu "tạm hết".</summary>
    [HttpDelete("{id:int}")]
    [InvalidateMenuCache]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await menu.DeleteAsync(id, ct); // 404 / 409 do MenuService ném exception
        return this.NoContent();        // 204: thành công, không có body
    }
}
