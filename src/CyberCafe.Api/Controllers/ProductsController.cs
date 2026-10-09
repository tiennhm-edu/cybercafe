// ============================================================================
// ProductsController.cs — CRUD thực đơn /api/products (Buổi 32–35 · REST CRUD;
//                         Buổi 36–41 · EF Core: AsNoTracking, lọc/sắp xếp/phân trang, projection;
//                         Buổi 42–47 · chỉ Admin được ghi, GET đi qua Redis cache, lỗi domain do handler toàn cục lo).
// Bảng mã trạng thái (ghi: thiếu token → 401, không phải Admin → 403):
//   GET    /api/products        200 + PagedResult        (400 nếu query sai)
//   GET    /api/products/{id}   200 | 404
//   POST   /api/products        201 + Location header | 400 ValidationProblem
//   PUT    /api/products/{id}   200 | 400 | 404
//   DELETE /api/products/{id}   204 | 404 | 409 (món đã có trong đơn)
// ============================================================================
using CyberCafe.Api.Auth;
using CyberCafe.Api.Caching;
using CyberCafe.Api.Data;
using CyberCafe.Api.Filters;
using CyberCafe.Api.Mapping;
using CyberCafe.Contracts.Common;
using CyberCafe.Contracts.Products;
using CyberCafe.Domain.Products;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CyberCafe.Api.Controllers;

/// <summary>Quản lý thực đơn.</summary>
// [ApiController]: tự validate DataAnnotations → 400 ValidationProblem; tự đọc body JSON cho tham số kiểu class.
[ApiController]
[Route("api/products")]
// Buổi 42–47: mặc định MỌI action cần policy ManageMenu (Admin); action đọc mở lại bằng [AllowAnonymous].
// "Đóng mặc định, mở có chủ đích" an toàn hơn "mở mặc định, nhớ đóng từng action".
[Authorize(Policy = Policies.ManageMenu)]
public class ProductsController(CyberCafeDbContext db, MenuCache menuCache) : ControllerBase
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
        // 👉 Bước 8 (b47.md): cache-aside. Key = query string đã chuẩn hóa (ProductQuery.ToQueryString)
        // → ?page=1&type=tea và ?type=tea&page=1 dùng CHUNG 1 key.
        CacheResult<PagedResult<ProductDto>> result = await menuCache.GetOrCreateAsync(
            $"products?{query.ToQueryString()}", token => this.LoadPageAsync(query, token), ct);
        this.Response.Headers[CacheHeader] = result.Hit ? "HIT" : "MISS";
        return result.Value;
    }

    // Truy vấn thật xuống database (chỉ chạy khi cache MISS)
    private async Task<PagedResult<ProductDto>> LoadPageAsync(ProductQuery query, CancellationToken ct)
    {
        // 👉 Bước 6 (b40.md): xây IQueryable từng bước — CHƯA chạy SQL cho tới ToListAsync/CountAsync.
        // AsNoTracking: chỉ đọc để trả JSON → EF không cần "theo dõi" thay đổi → nhanh hơn, ít RAM hơn.
        IQueryable<Product> source = db.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string keyword = query.Search.Trim();
            // SQL Server: LIKE N'%keyword%' với collation không phân biệt hoa/thường.
            // ⚠️ EF InMemory (test) thì PHÂN BIỆT hoa/thường — test nhớ gõ đúng chữ hoa.
            source = source.Where(p => p.Name.Contains(keyword));
        }

        // "p is Coffee" → WHERE ProductType = N'coffee' (lọc trên cột discriminator)
        source = query.Type switch
        {
            ProductTypes.Coffee => source.Where(p => p is Coffee),
            ProductTypes.Tea => source.Where(p => p is Tea),
            ProductTypes.Cake => source.Where(p => p is Cake),
            _ => source
        };

        // bool? : null = không lọc; true/false = lọc theo cột IsAvailable
        if (query.Available is not null)
        {
            source = source.Where(p => p.IsAvailable == query.Available.Value);
        }

        // Đếm TRƯỚC khi Skip/Take: TotalCount là tổng sau lọc, không phải số món của 1 trang.
        int total = await source.CountAsync(ct);

        // Sắp xếp phải có trước Skip/Take — không có ORDER BY thì "trang 2" mỗi lần chạy có thể khác nhau.
        source = (query.SortBy, query.Desc) switch
        {
            ("name", false) => source.OrderBy(p => p.Name),
            ("name", true) => source.OrderByDescending(p => p.Name),
            ("price", false) => source.OrderBy(p => p.Price).ThenBy(p => p.Id),
            ("price", true) => source.OrderByDescending(p => p.Price).ThenBy(p => p.Id),
            (_, true) => source.OrderByDescending(p => p.Id),
            _ => source.OrderBy(p => p.Id)
        };

        List<ProductDto> items = await source
            .Skip((query.Page - 1) * query.PageSize)   // OFFSET
            .Take(query.PageSize)                       // FETCH NEXT
            .Select(ProductMapping.ToDtoExpression)     // projection: chỉ SELECT cột cần cho DTO
            .ToListAsync(ct);

        return new PagedResult<ProductDto>(items, query.Page, query.PageSize, total);
    }

    /// <summary>Chi tiết 1 món.</summary>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType<ProductDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> GetById(int id, CancellationToken ct)
    {
        CacheResult<ProductDto?> result = await menuCache.GetOrCreateAsync($"product:{id}", token => db.Products
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(ProductMapping.ToDtoExpression)
            .FirstOrDefaultAsync(token), ct);
        this.Response.Headers[CacheHeader] = result.Hit ? "HIT" : "MISS";
        ProductDto? product = result.Value;

        // NotFound() trong [ApiController] tự trả body ProblemDetails (type, title, status, traceId)
        return product is null ? this.NotFound() : product;
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
        // Domain validate lần nữa (tên rỗng, giá âm...) và ném ArgumentException.
        // Buổi 32–41 bắt bằng try/catch ở đây; Buổi 42–47 để DomainExceptionHandler đổi thành 400.
        Product product = request.ToDomain();

        // Add chỉ đánh dấu trạng thái Added trong bộ nhớ; SaveChanges mới gửi SQL
        db.Products.Add(product);
        await db.SaveChangesAsync(ct); // INSERT ... ; SELECT SCOPE_IDENTITY() → product.Id có giá trị

        // 201 Created + header Location: /api/products/{id} (client biết URL của tài nguyên vừa tạo)
        // ⚠️ Lỗi hay gặp: nameof(GetByIdAsync) — ASP.NET Core bỏ hậu tố "Async" khỏi tên action → không tìm thấy route.
        return this.CreatedAtAction(nameof(this.GetById), new { id = product.Id }, product.ToDto());
    }

    /// <summary>Sửa món, không đổi được loại món (Admin).</summary>
    [HttpPut("{id:int}")]
    [InvalidateMenuCache]
    [ProducesResponseType<ProductDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> Update(int id, ProductRequest request, CancellationToken ct)
    {
        // KHÔNG AsNoTracking: cần EF theo dõi object để biết cột nào đổi khi SaveChanges
        Product? product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null)
        {
            return this.NotFound();
        }

        // TPH: đổi Coffee → Cake nghĩa là đổi CLASS của object — EF không hỗ trợ "đổi kiểu" tại chỗ.
        if (ProductMapping.TypeOf(product) != request.Type)
        {
            this.ModelState.AddModelError(nameof(request.Type), "Không đổi được loại món. Hãy tạo món mới.");
            return this.ValidationProblem(this.ModelState);
        }

        request.ApplyTo(product); // ArgumentException từ domain → 400 (DomainExceptionHandler)

        await db.SaveChangesAsync(ct); // UPDATE Products SET Name = @p0, Price = @p1 ... WHERE Id = @p2
        return product.ToDto();
    }

    /// <summary>Xóa món chưa từng được bán (Admin). Món đã có trong đơn → 409, hãy đánh dấu "tạm hết".</summary>
    [HttpDelete("{id:int}")]
    [InvalidateMenuCache]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        Product? product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null)
        {
            return this.NotFound();
        }

        // EF.Property<int>(i, "ProductId"): truy vấn trên SHADOW PROPERTY (cột khóa ngoại không có trong class)
        bool sold = await db.OrderItems.AnyAsync(i => EF.Property<int>(i, "ProductId") == id, ct);
        if (sold)
        {
            return this.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Không xóa được món",
                detail: $"{product.Name} đã có trong đơn hàng. Hãy đánh dấu tạm hết thay vì xóa.");
        }

        db.Products.Remove(product);
        await db.SaveChangesAsync(ct); // DELETE FROM Products WHERE Id = @p0
        return this.NoContent();       // 204: thành công, không có body
    }
}
