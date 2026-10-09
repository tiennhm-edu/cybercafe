// ============================================================================
// ApiExceptions.cs — exception "có chủ đích" của tầng Api (Buổi 42–47).
// AuthService ném exception (sai mật khẩu → 401, email trùng → 409);
// DomainExceptionHandler đổi thành ProblemDetails → service không phải biết HTTP.
// Vì sao không ném InvalidOperationException như Domain? Handler chỉ tin InvalidOperationException
// ném từ Domain (lỗi khác cùng kiểu từ EF/thư viện là bug → 500). Ở Api thì dùng kiểu riêng, rõ nghĩa.
// ============================================================================
namespace CyberCafe.Api.Errors;

/// <summary>Đăng nhập sai / token không hợp lệ → 401.</summary>
/// <param name="message">Thông báo an toàn để trả cho client (không lộ "email có tồn tại hay không").</param>
public class AuthenticationFailedException(string message) : Exception(message);

/// <summary>Xung đột dữ liệu ở tầng Api (vd email đã được đăng ký) → 409.</summary>
/// <param name="message">Thông báo hiển thị cho người dùng.</param>
public class ConflictException(string message) : Exception(message);
