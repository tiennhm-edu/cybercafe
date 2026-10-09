// ============================================================================
// LoggingBehavior.cs — log mỗi command/query: tên, thời gian, kết quả (Buổi 52 · pipeline behavior).
// Đứng NGOÀI CÙNG pipeline → đo được cả thời gian validate + transaction, và log được cả request bị từ chối.
// Khác RequestLoggingMiddleware (b42): middleware log theo URL ("POST /api/orders"), behavior log theo
// Ý ĐỊNH NGHIỆP VỤ ("PlaceOrderCommand") — kể cả khi command đến từ hub hay background job, không qua HTTP.
// ⚠️ Lỗi hay gặp: log nguyên object request → lộ số thẻ (PlaceOrderCommand.CardNumber) vào file log. Chỉ log TÊN kiểu.
// ============================================================================
using System.Diagnostics;
using CyberCafe.Application.Common.Messaging;
using Microsoft.Extensions.Logging;

namespace CyberCafe.Application.Common.Behaviors;

// 👉 Bước 2 (b52.md)
/// <summary>Ghi log trước/sau mỗi request; lỗi thì log cảnh báo rồi ném tiếp (không nuốt lỗi).</summary>
public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <inheritdoc />
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        string name = typeof(TRequest).Name;
        long start = Stopwatch.GetTimestamp();
        logger.LogInformation("→ {Request}", name);
        try
        {
            TResponse response = await next(ct);
            logger.LogInformation("✓ {Request} xong trong {Elapsed:0} ms", name, Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            // Information/Warning chứ không Error: phần lớn là lỗi nghiệp vụ có chủ đích (409, 404) — bug thật
            // sẽ được exception handler của Api log Error với stack trace đầy đủ.
            logger.LogWarning("✗ {Request} thất bại sau {Elapsed:0} ms: {Error}", name, Stopwatch.GetElapsedTime(start).TotalMilliseconds, ex.GetType().Name);
            throw;
        }
    }
}
