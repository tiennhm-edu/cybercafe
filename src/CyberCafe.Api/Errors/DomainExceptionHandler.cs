// ============================================================================
// DomainExceptionHandler.cs — xử lý exception TOÀN CỤC → ProblemDetails (Buổi 42–47 · 48).
// .NET 8+ có sẵn IExceptionHandler + app.UseExceptionHandler(): không cần tự viết try/catch middleware
// như lab webapi b42 (GlobalExceptionMiddleware) — cùng ý tưởng, ít code hơn.
// Nhờ handler này, controller bỏ được các khối try/catch lặp lại của b40.
// Bảng ánh xạ:
//   AuthenticationFailedException (Application)    → 401
//   ValidationException (Application, b48)         → 400 ValidationProblem { errors: { field: [...] } }
//   NotFoundException (Application, b48)           → 404 (body giống NotFound() của [ApiController])
//   ConflictException (Application)                → 409 (title riêng: "Không xóa được món", "Không hủy được đơn"...)
//   ArgumentException      ném từ Domain/Contracts → 400 (dữ liệu vi phạm luật domain)
//   InvalidOperationException ném từ Domain        → 409 (xung đột trạng thái: món hết, sai luồng đơn...)
//   Mọi exception khác (lỗi EF, bug...)            → return false → handler mặc định trả 500 (không lộ chi tiết)
// Buổi 48: use case ở Application ném exception có NGHĨA (không biết HTTP); file này là nơi DUY NHẤT dịch sang mã HTTP.
// ============================================================================
using CyberCafe.Application.Common.Exceptions;
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
        // 👉 Bước 7 (b47.md) · 👉 Bước 4 (b48.md)
        ProblemDetails? problem = exception switch
        {
            AuthenticationFailedException => Problem(StatusCodes.Status401Unauthorized, "Xác thực thất bại", exception.Message),
            // HttpValidationProblemDetails: cùng hình dạng ValidationProblem của [ApiController] → Web đọc "errors" như cũ
            ValidationException validation => new HttpValidationProblemDetails(validation.Errors.ToDictionary())
            {
                Status = StatusCodes.Status400BadRequest,
            },
            // Title/Detail để trống → ProblemDetails mặc định điền "Not Found" + type RFC 9110, giống hệt b47
            NotFoundException => new ProblemDetails { Status = StatusCodes.Status404NotFound },
            ConflictException conflict => Problem(StatusCodes.Status409Conflict, conflict.Title, conflict.Message),
            ArgumentException when IsFromDomain(exception) => Problem(StatusCodes.Status400BadRequest, "Dữ liệu không hợp lệ", exception.Message),
            InvalidOperationException when IsFromDomain(exception) => Problem(StatusCodes.Status409Conflict, "Vi phạm quy tắc nghiệp vụ", exception.Message),
            _ => null
        };

        if (problem is null)
        {
            return false; // ⚠️ Không "nuốt" mọi exception thành 400/409: bug thật phải lộ ra là 500 + log Error
        }

        logger.LogInformation("{Type}: {Message}", exception.GetType().Name, exception.Message);
        httpContext.Response.StatusCode = problem.Status!.Value;

        // IProblemDetailsService: ghi JSON application/problem+json, tự áp CustomizeProblemDetails (correlationId)
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem,
        });
    }

    // Thông báo của Domain/Application viết sẵn bằng tiếng Việt, an toàn để hiển thị → đưa vào "detail"
    private static ProblemDetails Problem(int status, string title, string detail) =>
        new() { Status = status, Title = title, Detail = detail };

    // Exception ném ra TỪ code Domain/Contracts (luật nghiệp vụ) hay từ thư viện khác (EF Core cũng ném
    // InvalidOperationException khi cấu hình sai — đó là bug, phải là 500 chứ không phải 409).
    private static bool IsFromDomain(Exception exception)
    {
        System.Reflection.Assembly? source = exception.TargetSite?.DeclaringType?.Assembly;
        return source == typeof(Order).Assembly || source == typeof(Contracts.Orders.OrderDto).Assembly;
    }
}
