// ============================================================================
// CurrentUser.cs — "ai đang gọi use case" ở dạng C# thuần (Buổi 48 · Clean Architecture).
// Buổi 42–47: controller tự đọc this.User (ClaimsPrincipal) rồi kiểm tra IsStaff / chủ đơn ngay trong action.
// Buổi 48: luật "nhân viên xem mọi đơn, khách chỉ đơn của mình" là NGHIỆP VỤ → chuyển vào Application
//   (b48: OrderService; b51–53: nằm trong command/query — CurrentUser đi kèm command như 1 trường dữ liệu).
//   Nhưng Application không được biết ClaimsPrincipal/HttpContext (ASP.NET Core) → Api đổi claims
//   thành record nhỏ này (ClaimsPrincipalExtensions.ToCurrentUser) rồi truyền vào.
// Cách khác hay gặp: interface ICurrentUserService + IHttpContextAccessor. Ở đây truyền THAM SỐ cho rõ
// và dùng được cả trong hub SignalR (Context.User) lẫn unit test (new CurrentUser(5, false)).
// ============================================================================
namespace CyberCafe.Application.Common;

/// <summary>Người đang gọi use case.</summary>
/// <param name="UserId">Id tài khoản (null = chưa đăng nhập).</param>
/// <param name="IsStaff">Barista hoặc Admin — được xem/xử lý MỌI đơn.</param>
public sealed record CurrentUser(int? UserId, bool IsStaff)
{
    /// <summary>
    /// Được thao tác bản ghi có chủ là <paramref name="ownerUserId"/> không:
    /// nhân viên luôn được; khách chỉ khi là chủ (chủ null = đơn cũ chưa gắn tài khoản → không ai là chủ).
    /// </summary>
    public bool CanAccess(int? ownerUserId) =>
        this.IsStaff || (ownerUserId is not null && ownerUserId == this.UserId);
}
