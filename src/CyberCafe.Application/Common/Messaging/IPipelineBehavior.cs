// ============================================================================
// IPipelineBehavior.cs — "middleware" cho command/query (Buổi 52 · pipeline behavior).
// Giống middleware ASP.NET Core (b42) nhưng bọc quanh HANDLER thay vì quanh HTTP request:
//   Send(cmd) → Logging → Validation → Transaction → Handler
//                  ◄─────────── kết quả đi ngược lại ───────────┘
// Việc "cắt ngang" (log, validate, transaction) viết 1 lần, áp cho MỌI command/query — handler chỉ lo nghiệp vụ.
// Ưu điểm so với middleware HTTP: chạy cả khi command được gửi từ hub SignalR, background job, unit test.
// ============================================================================
namespace CyberCafe.Application.Common.Messaging;

/// <summary>Gọi bước tiếp theo trong pipeline (behavior kế tiếp, hoặc handler nếu là bước cuối).</summary>
/// <typeparam name="TResponse">Kiểu kết quả.</typeparam>
public delegate Task<TResponse> RequestHandlerDelegate<TResponse>(CancellationToken ct);

// 👉 Bước 1 (b52.md)
/// <summary>1 lớp bọc quanh handler. Đăng ký nhiều behavior → thứ tự đăng ký = thứ tự từ ngoài vào trong.</summary>
/// <typeparam name="TRequest">Kiểu command/query.</typeparam>
/// <typeparam name="TResponse">Kiểu kết quả.</typeparam>
public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Làm gì đó TRƯỚC, gọi <paramref name="next"/>, làm gì đó SAU.
    /// ⚠️ Lỗi hay gặp: quên gọi next → handler không bao giờ chạy, command "thành công" mà không làm gì.
    /// </summary>
    Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct);
}
