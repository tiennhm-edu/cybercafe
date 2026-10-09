// ============================================================================
// CorrelationIdMiddleware.cs — gắn "mã vết" cho mỗi request (Buổi 42–47 · middleware tự viết).
// Khách báo lỗi kèm mã X-Correlation-Id → grep log ra ĐÚNG các dòng của request đó.
// Web gửi sẵn header này (BearerTokenHandler) → 1 mã xuyên suốt Web → Api.
// Dạng middleware CLASS theo quy ước: constructor nhận RequestDelegate next + method InvokeAsync(HttpContext).
// So với lab webapi b42: ở đó correlation id là middleware inline (app.Use(...)); ở đây tách class để test/đọc dễ hơn.
// ============================================================================
namespace CyberCafe.Api.Middleware;

/// <summary>Đọc X-Correlation-Id từ request (hoặc tạo mới), trả lại trong response và đưa vào log scope.</summary>
public class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    /// <summary>Tên header.</summary>
    public const string HeaderName = "X-Correlation-Id";

    /// <summary>Key trong HttpContext.Items để các phần sau (ProblemDetails) đọc lại.</summary>
    public const string ItemKey = "CorrelationId";

    /// <summary>Middleware được gọi 1 lần cho MỖI request.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        // 👉 Bước 6 (b47.md)
        // Chỉ nhận mã "sạch" từ client (≤ 64 ký tự chữ/số/-) — không để client chèn chuỗi rác vào log
        string? incoming = context.Request.Headers[HeaderName].FirstOrDefault();
        string correlationId = incoming is { Length: > 0 and <= 64 } && incoming.All(c => char.IsLetterOrDigit(c) || c == '-')
            ? incoming
            : Guid.NewGuid().ToString("N");

        context.Items[ItemKey] = correlationId;

        // OnStarting: gắn header NGAY TRƯỚC khi response bắt đầu gửi (sau đó header bị khóa)
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        // BeginScope: mọi dòng log trong request này tự kèm CorrelationId (khi provider log hỗ trợ scope)
        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await next(context); // ⚠️ Lỗi hay gặp: quên gọi next → request dừng ở đây, client nhận 200 rỗng
        }
    }
}
