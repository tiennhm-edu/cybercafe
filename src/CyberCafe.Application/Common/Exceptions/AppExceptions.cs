// ============================================================================
// AppExceptions.cs — exception "có chủ đích" của tầng Application (Buổi 42–47 · 48).
// Buổi 42–47: file này là Api/Errors/ApiExceptions.cs (chỉ có 401 + 409).
// Buổi 48: chuyển xuống Application vì use case (MenuService, OrderService, AuthService) mới là nơi
//   phát hiện "không tìm thấy", "dữ liệu sai", "trùng" — nhưng use case KHÔNG được biết HTTP.
//   → Ném exception mang NGHĨA nghiệp vụ; DomainExceptionHandler (Api) dịch sang mã HTTP:
//     NotFoundException → 404 · ValidationException → 400 · ConflictException → 409 · AuthenticationFailed → 401
// Vì sao không ném InvalidOperationException như Domain? Handler chỉ tin InvalidOperationException
// ném từ Domain (lỗi cùng kiểu từ EF/thư viện là bug → 500). Ở đây dùng kiểu riêng, rõ nghĩa.
// ⚠️ Lỗi hay gặp: service trả về IActionResult / NotFound() → Application dính ASP.NET Core, test khó.
// ============================================================================
namespace CyberCafe.Application.Common.Exceptions;

// 👉 Bước 4 (b48.md)
/// <summary>Đăng nhập sai / token không hợp lệ → 401.</summary>
/// <param name="message">Thông báo an toàn để trả cho client (không lộ "email có tồn tại hay không").</param>
public class AuthenticationFailedException(string message) : Exception(message);

/// <summary>Xung đột dữ liệu / trạng thái (email đã đăng ký, món đã bán, khách hủy đơn đang pha...) → 409.</summary>
/// <param name="message">Thông báo hiển thị cho người dùng (thành "detail" của ProblemDetails).</param>
/// <param name="title">Tiêu đề ngắn (thành "title" của ProblemDetails).</param>
public class ConflictException(string message, string title = "Dữ liệu bị trùng") : Exception(message)
{
    /// <summary>Tiêu đề ProblemDetails.</summary>
    public string Title { get; } = title;
}

/// <summary>Không có bản ghi cần thao tác (hoặc có nhưng người gọi không được thấy — chống IDOR) → 404.</summary>
/// <param name="message">Thông báo để ghi log (KHÔNG trả về client: 404 giữ nguyên body mặc định như b47).</param>
public class NotFoundException(string message) : Exception(message);

/// <summary>
/// Dữ liệu vào sai luật nghiệp vụ (món không tồn tại, mã giảm giá sai...) → 400 ValidationProblem,
/// cùng dạng { "errors": { "Items": ["..."] } } mà [ApiController] trả khi DataAnnotations sai.
/// </summary>
public class ValidationException : Exception
{
    /// <summary>Tạo exception với danh sách lỗi theo field.</summary>
    public ValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("Dữ liệu không hợp lệ")
    {
        this.Errors = errors;
    }

    /// <summary>Lỗi theo tên field (khớp tên property của request để Web hiện đúng chỗ).</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    /// <summary>Viết tắt cho trường hợp 1 field 1 lỗi.</summary>
    public static ValidationException For(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}
