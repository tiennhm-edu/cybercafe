// ============================================================================
// BlazorAuthorizationResultHandler.cs — cho trang [Authorize] đi qua tầng HTTP (Buổi 42–47).
// Vì sao cần? Trang có [Authorize] cũng gắn metadata lên endpoint HTTP. Khi F5, trình duyệt gửi request
// thường tới Web — token nằm trong sessionStorage (mã hóa) nên request đó KHÔNG mang token
// → middleware Authorization tưởng chưa đăng nhập và chặn (lỗi "No authenticationScheme was specified").
// Handler này cho request tới TRANG Razor đi tiếp; kiểm tra quyền thật do AuthorizeRouteView làm
// TRONG circuit (nơi đọc được token). Dữ liệu thật vẫn do Api (JWT) bảo vệ. Cùng cách với lab blazor b34.
// ============================================================================
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Components.Endpoints;

namespace CyberCafe.Web.Services.Auth;

/// <summary>Bỏ qua kết quả authorize ở tầng HTTP cho endpoint là trang Razor component.</summary>
public sealed class BlazorAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    // Handler mặc định — vẫn dùng cho mọi endpoint KHÔNG phải trang Razor
    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();

    /// <inheritdoc />
    public Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        // ComponentTypeMetadata chỉ có trên endpoint sinh từ trang Razor (@page)
        bool isComponentPage = context.GetEndpoint()?.Metadata.GetMetadata<ComponentTypeMetadata>() is not null;
        return isComponentPage
            ? next(context)
            : this._defaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }
}
