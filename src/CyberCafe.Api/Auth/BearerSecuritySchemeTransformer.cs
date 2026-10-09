// ============================================================================
// BearerSecuritySchemeTransformer.cs — khai báo JWT Bearer trong tài liệu OpenAPI (Buổi 42–47).
// Có khai báo này, Scalar (/scalar/v1) hiện ô nhập token → thử các endpoint cần đăng nhập ngay trên trình duyệt.
// Cùng cách làm với lab webapi b46.
// ============================================================================
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CyberCafe.Api.Auth;

/// <summary>Thêm security scheme HTTP Bearer (JWT) vào /openapi/v1.json.</summary>
public sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    /// <summary>Tên scheme trong tài liệu.</summary>
    public const string SchemeName = "Bearer";

    /// <inheritdoc />
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Dán accessToken lấy từ POST /api/auth/login (không cần gõ chữ 'Bearer').",
        };

        // Áp dụng cho cả tài liệu: endpoint [AllowAnonymous] vẫn gọi được khi không có token
        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(SchemeName, document)] = [],
        });
        return Task.CompletedTask;
    }
}
