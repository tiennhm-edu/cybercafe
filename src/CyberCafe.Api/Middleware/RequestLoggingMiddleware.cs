// ============================================================================
// RequestLoggingMiddleware.cs — ghi 1 dòng log cho mỗi request (Buổi 42–47).
//   "HTTP POST /api/orders → 201 trong 35 ms (cid=…)"
// Đặt NGOÀI exception handler trong pipeline → đo được cả request lỗi 500 (đã thành ProblemDetails).
// ============================================================================
using System.Diagnostics;

namespace CyberCafe.Api.Middleware;

/// <summary>Log method, path, mã trạng thái, thời gian xử lý.</summary>
public class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    /// <summary>Đo thời gian quanh phần còn lại của pipeline.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        long start = Stopwatch.GetTimestamp();
        try
        {
            await next(context);
        }
        finally
        {
            // finally: kể cả khi phía sau ném exception vẫn ghi được log
            double elapsedMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            // ⚠️ Lỗi hay gặp: log cả QueryString → lộ access_token của SignalR (?access_token=...) vào file log.
            //    Chỉ log Path.
            logger.LogInformation("HTTP {Method} {Path} → {StatusCode} trong {Elapsed:0} ms (cid={CorrelationId})",
                context.Request.Method,
                context.Request.Path.Value,
                context.Response.StatusCode,
                elapsedMs,
                context.Items[CorrelationIdMiddleware.ItemKey]);
        }
    }
}
