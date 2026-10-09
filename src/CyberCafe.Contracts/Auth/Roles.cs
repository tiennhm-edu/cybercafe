// ============================================================================
// Roles.cs — vai trò người dùng + tên claim trong JWT (Buổi 42–47 · Authorization).
// Dùng CHUNG ở 3 nơi: Api ([Authorize(Roles = ...)], phát token), Web (<AuthorizeView Roles=...>,
// [Authorize] trên trang), Test. Attribute chỉ nhận hằng số (const) → gom tên vào đây,
// gõ sai "admin"/"Admin" là lỗi biên dịch thay vì 403 khó hiểu lúc chạy.
// ============================================================================
namespace CyberCafe.Contracts.Auth;

/// <summary>3 vai trò của CyberCafe. Lưu trong cột Users.Role dạng chuỗi.</summary>
public enum UserRole
{
    /// <summary>Khách: đặt đơn, xem/hủy đơn của chính mình.</summary>
    Customer,

    /// <summary>Nhân viên pha chế: xem mọi đơn đang chạy, đổi trạng thái.</summary>
    Barista,

    /// <summary>Quản trị: quản lý thực đơn, xem báo cáo (và làm được việc của barista).</summary>
    Admin
}

/// <summary>Tên role dạng hằng số cho [Authorize(Roles = ...)].</summary>
public static class Roles
{
    /// <summary>"Customer".</summary>
    public const string Customer = nameof(UserRole.Customer);

    /// <summary>"Barista".</summary>
    public const string Barista = nameof(UserRole.Barista);

    /// <summary>"Admin".</summary>
    public const string Admin = nameof(UserRole.Admin);

    /// <summary>Dấu phẩy = HOẶC: Barista hoặc Admin.</summary>
    public const string Staff = Barista + "," + Admin;
}

/// <summary>
/// Tên claim tự đặt ngắn gọn (không dùng URI dài kiểu http://schemas.xmlsoap.org/...).
/// Api ghi các claim này vào JWT; Web đọc lại để biết tên + vai trò người dùng.
/// </summary>
public static class AppClaimTypes
{
    /// <summary>Id người dùng (chuẩn JWT "sub").</summary>
    public const string UserId = "sub";

    /// <summary>Email đăng nhập.</summary>
    public const string Email = "email";

    /// <summary>Họ tên hiển thị → User.Identity.Name.</summary>
    public const string Name = "name";

    /// <summary>Vai trò → User.IsInRole(...), [Authorize(Roles = ...)].</summary>
    public const string Role = "role";
}
