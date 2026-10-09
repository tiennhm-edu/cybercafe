// ============================================================================
// MenuQueries.cs — query đọc thực đơn qua cache (Buổi 51 · query; Buổi 53 · read model + cache).
//   GetMenuQuery         GET /api/products (lọc, sắp xếp, phân trang) — đi qua Redis
//   GetProductByIdQuery  GET /api/products/{id}                      — đi qua Redis
// Trả CacheResult<T> (giá trị + HIT/MISS) để controller gắn header X-Cache như b47.
// GHI thực đơn (thêm/sửa/xóa) vẫn ở MenuService (b48) + filter [InvalidateMenuCache]: CRUD đơn giản, không
// có aggregate/invariant phức tạp → không cần command riêng. CQRS KHÔNG phải "tất cả hoặc không gì cả"
// (xem ADR 0003).
// ============================================================================
using CyberCafe.Application.Caching;
using CyberCafe.Application.Common.Messaging;
using CyberCafe.Contracts.Common;
using CyberCafe.Contracts.Products;
using FluentValidation;

namespace CyberCafe.Application.Products.Queries;

// 👉 Bước 3 (b53.md)
/// <summary>1 trang thực đơn (tham số y hệt query string của GET /api/products).</summary>
/// <param name="Filter">Bộ lọc/sắp xếp/phân trang (Contracts — Web dựng cùng class này).</param>
public sealed record GetMenuQuery(ProductQuery Filter) : IQuery<CacheResult<PagedResult<ProductDto>>>;

/// <summary>Chi tiết 1 món (Value = null nếu không có).</summary>
/// <param name="ProductId">Id món.</param>
public sealed record GetProductByIdQuery(int ProductId) : IQuery<CacheResult<ProductDto?>>;

/// <summary>Luật tham số thực đơn — trùng DataAnnotations của ProductQuery, chạy cả khi không qua HTTP.</summary>
public sealed class GetMenuQueryValidator : AbstractValidator<GetMenuQuery>
{
    private static readonly string[] SortFields = ["id", "name", "price"];

    /// <summary>Khai báo luật.</summary>
    public GetMenuQueryValidator()
    {
        this.RuleFor(q => q.Filter.Page).InclusiveBetween(1, 10_000).WithMessage("page phải ≥ 1");
        this.RuleFor(q => q.Filter.PageSize).InclusiveBetween(1, ProductQuery.MaxPageSize).WithMessage("pageSize từ 1 đến 100");
        this.RuleFor(q => q.Filter.SortBy).Must(s => SortFields.Contains(s)).WithMessage("sortBy phải là id, name hoặc price");
        this.RuleFor(q => q.Filter.Type).Must(t => t is null || ProductTypes.All.Contains(t))
            .WithMessage("Loại món phải là coffee, tea hoặc cake");
        this.RuleFor(q => q.Filter.Search).MaximumLength(50);
    }
}

/// <summary>Cache-aside: key = query string chuẩn hóa; MISS thì đọc qua IProductRepository (projection).</summary>
public sealed class MenuQueryHandlers(IProductRepository products, IMenuCache menuCache) :
    IQueryHandler<GetMenuQuery, CacheResult<PagedResult<ProductDto>>>,
    IQueryHandler<GetProductByIdQuery, CacheResult<ProductDto?>>
{
    /// <inheritdoc />
    public Task<CacheResult<PagedResult<ProductDto>>> Handle(GetMenuQuery query, CancellationToken ct) =>
        // ?page=1&type=tea và ?type=tea&page=1 dùng CHUNG 1 key nhờ ToQueryString() có thứ tự cố định
        menuCache.GetOrCreateAsync($"products?{query.Filter.ToQueryString()}", token => products.GetPageAsync(query.Filter, token), ct);

    /// <inheritdoc />
    public Task<CacheResult<ProductDto?>> Handle(GetProductByIdQuery query, CancellationToken ct) =>
        menuCache.GetOrCreateAsync($"product:{query.ProductId}", token => products.GetDtoAsync(query.ProductId, token), ct);
}
