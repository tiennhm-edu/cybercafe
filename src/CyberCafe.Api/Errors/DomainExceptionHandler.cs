// ============================================================================
// DomainExceptionHandler.cs — xử lý exception TOÀN CỤC → ProblemDetails (Buổi 42–47).
// .NET 8+ có sẵn IExceptionHandler + app.UseExceptionHandler(): không cần tự viết try/catch middleware
// như lab webapi b42 (GlobalExceptionMiddleware) — cùng ý tưởng, ít code hơn.
// Nhờ handler này, controller bỏ được các khối try/catch lặp lại của b40.
// Bảng ánh xạ:
//   AuthenticationFailedException                  → 401
//   ConflictException (Api, vd email trùng)        → 409
//   ArgumentException      ném từ Domain/Contracts → 400 (dữ liệu vi phạm luật domain)
//   InvalidOperationException ném từ Domain        → 409 (xung đột trạng thái: món hết, sai luồng đơn...)
//   Mọi exception khác (lỗi EF, bug...)            → return false → handler mặc định trả 500 (không lộ chi tiết)
// ============================================================================
using CyberCafe.Domain.Orders;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CyberCafe.Api.Errors;

/// <summary>Đổi exception "có nghĩa nghiệp vụ" thành mã HTTP phù hợp.</summary>
public class DomainExceptionHandler(IProblemDetailsService problemDetails, ILogger<DomainExceptionHandler> logger) : IExceptionHandler
{
    /// <summary>Trả true nếu đã xử lý (đã ghi response), false để handler kế tiếp/mặc định lo.</summary>
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // 👉 Bước 7 (b47.md)
        (int Status, string Title)? mapped = exception switch
        {
            AuthenticationFailedException => (StatusCodes.Status401Unauthorized, "Xác thực thất bại"),
            ConflictException => (StatusCodes.Status409Conflict, "Dữ liệu bị trùng"),
            ArgumentException when IsFromDomain(exception) => (StatusCodes.Status400BadRequest, "Dữ liệu không hợp lệ"),
            InvalidOperationException when IsFromDomain(exception) => (StatusCodes.Status409Conflict, "Vi phạm quy tắc nghiệp vụ"),
            _ => null
        };

        if (mapped is null)
        {
            return false; // ⚠️ Không "nuốt" mọi exception thành 400/409: bug thật phải lộ ra là 500 + log Error
        }

        logger.LogInformation("{Type}: {Message}", exception.GetType().Name, exception.Message);
        httpContext.Response.StatusCode = mapped.Value.Status;

        // IProblemDetailsService: ghi JSON application/problem+json, tự áp CustomizeProblemDetails (correlationId)
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = mapped.Value.Status,
                Title = mapped.Value.Title,
                Detail = exception.Message, // thông báo domain viết sẵn bằng tiếng Việt, an toàn để hiển thị
            },
        });
    }

    // Exception ném ra TỪ code Domain/Contracts (luật nghiệp vụ) hay từ thư viện khác (EF Core cũng ném
    // InvalidOperationException khi cấu hình sai — đó là bug, phải là 500 chứ không phải 409).
    private static bool IsFromDomain(Exception exception)
    {
        System.Reflection.Assembly? source = exception.TargetSite?.DeclaringType?.Assembly;
        return source == typeof(Order).Assembly || source == typeof(Contracts.Orders.OrderDto).Assembly;
    }
}
