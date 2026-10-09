// ============================================================================
// ValidationBehavior.cs — chạy mọi validator của request TRƯỚC handler (Buổi 52 · validation).
// Validator viết bằng FluentValidation (Apache-2.0): mỗi command/query có class XxxValidator : AbstractValidator<Xxx>.
// Không có validator nào → đi thẳng qua. Có lỗi → ném ValidationException (Application) → Api trả 400
// ValidationProblem cùng hình dạng { "errors": { "CustomerName": ["..."] } } như [ApiController].
// "Đã có DataAnnotations ở Api rồi sao còn validate?" — command có thể đến từ chỗ KHÔNG qua [ApiController]
// (hub, job, test, app khác). Use case tự bảo vệ đầu vào của nó. Luật nghiệp vụ sâu (món có tồn tại, đơn
// đã thanh toán chưa) vẫn là việc của handler/aggregate — validator chỉ kiểm tra HÌNH DẠNG dữ liệu.
// ============================================================================
using CyberCafe.Application.Common.Messaging;
using FluentValidation;
using FluentValidation.Results;
// ⚠️ Lỗi hay gặp: FluentValidation cũng có class tên ValidationException → "ambiguous reference". Đặt alias rõ ràng:
using ValidationException = CyberCafe.Application.Common.Exceptions.ValidationException;

namespace CyberCafe.Application.Common.Behaviors;

// 👉 Bước 3 (b52.md)
/// <summary>Validate request bằng mọi IValidator&lt;TRequest&gt; đã đăng ký; sai → ValidationException (→ 400).</summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <inheritdoc />
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        List<ValidationFailure> failures = [];
        foreach (IValidator<TRequest> validator in validators)
        {
            ValidationResult result = await validator.ValidateAsync(request, ct);
            failures.AddRange(result.Errors);
        }

        if (failures.Count > 0)
        {
            // Gom theo tên field: "Items[0].Quantity" → ["Số lượng từ 1 đến 20"] — khớp cách Web đọc "errors"
            Dictionary<string, string[]> errors = failures
                .GroupBy(f => f.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).Distinct().ToArray());
            throw new ValidationException(errors); // KHÔNG gọi next → handler không chạy, DB không bị đụng tới
        }

        return await next(ct);
    }
}
