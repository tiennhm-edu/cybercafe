// ============================================================================
// DomainException.cs — aggregate từ chối 1 thao tác vi phạm luật nghiệp vụ (Buổi 49 · invariant).
// Ví dụ: sửa món sau khi đã thanh toán, thanh toán 2 lần, pha chế đơn chưa trả tiền, Pending → Ready.
// Kế thừa InvalidOperationException ("thao tác không hợp lệ với trạng thái hiện tại") nên code cũ bắt
// InvalidOperationException vẫn chạy; DomainExceptionHandler (Api) map riêng kiểu này → 409 Conflict.
// ⚠️ Lỗi hay gặp: ném DomainException cho dữ liệu SAI ĐỊNH DẠNG (SĐT thiếu số) — đó là ArgumentException (→ 400).
//    DomainException dành cho "dữ liệu đúng nhưng LÚC NÀY không được làm".
// ============================================================================
namespace CyberCafe.Domain.Common;

// 👉 Bước 1 (b49.md)
/// <summary>Vi phạm luật nghiệp vụ / invariant của aggregate (→ HTTP 409).</summary>
/// <param name="message">Thông báo tiếng Việt, hiển thị được cho người dùng.</param>
public class DomainException(string message) : InvalidOperationException(message);
