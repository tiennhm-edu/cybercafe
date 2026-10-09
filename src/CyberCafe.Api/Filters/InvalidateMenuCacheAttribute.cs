// ============================================================================
// InvalidateMenuCacheAttribute.cs — ACTION FILTER xóa cache thực đơn sau khi ghi (Buổi 42–47 · 48 · MVC filter).
// Vì sao filter mà không gọi MenuCache.InvalidateAsync() trong từng action?
//   POST/PUT/DELETE món đều cần "ghi xong → xóa cache". Gắn [InvalidateMenuCache] lên action
//   → không ai quên, action chỉ lo nghiệp vụ. Đây là việc "cắt ngang" (cross-cutting) gắn với
//   ACTION cụ thể → hợp với filter hơn middleware (middleware không biết action nào ghi menu).
// Đối chiếu lab webapi b43: ResourceCacheAttribute (cache), LogActionFilter (→ ở đây là middleware),
//   ValidationFilter (→ ở đây dùng sẵn [ApiController]), DomainExceptionFilter (→ IExceptionHandler).
// Buổi 48: inject IMenuCache (interface ở Application) thay vì class MenuCache cụ thể.
//   MenuService ném NotFoundException/ConflictException → executed.Exception khác null → KHÔNG xóa cache (đúng).
// ============================================================================
using CyberCafe.Application.Caching;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace CyberCafe.Api.Filters;

/// <summary>
/// Gắn lên action ghi thực đơn. TypeFilterAttribute: filter thật (<see cref="InvalidateMenuCacheFilter"/>)
/// được tạo qua DI nên inject được IMenuCache — attribute thường thì không có constructor injection.
/// </summary>
public sealed class InvalidateMenuCacheAttribute() : TypeFilterAttribute(typeof(InvalidateMenuCacheFilter));

/// <summary>Chạy SAU action: kết quả 2xx → vô hiệu cache thực đơn.</summary>
public sealed class InvalidateMenuCacheFilter(IMenuCache menuCache) : IAsyncActionFilter
{
    /// <inheritdoc />
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // 👉 Bước 9 (b47.md): code TRƯỚC next() = trước action; SAU next() = sau action (chưa ghi response)
        ActionExecutedContext executed = await next();

        // Chỉ xóa cache khi ghi THÀNH CÔNG: 400/404/409 không đổi dữ liệu → giữ cache.
        // return dto; (ActionResult<T>) → ObjectResult có StatusCode null = 200.
        bool succeeded = executed.Exception is null
            && executed.Result is IStatusCodeActionResult { StatusCode: null or (>= 200 and < 300) };
        if (succeeded)
        {
            await menuCache.InvalidateAsync(context.HttpContext.RequestAborted);
        }
    }
}
