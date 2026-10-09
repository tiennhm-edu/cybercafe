// ============================================================================
// Policies.cs — authorization POLICY theo nghiệp vụ (Buổi 42–47).
// [Authorize(Roles = "Admin")] rải khắp controller → đổi luật phải sửa nhiều chỗ.
// Policy đặt TÊN theo việc được làm ("ManageMenu"), còn "ai được làm" khai báo 1 chỗ ở đây.
//   ManageMenu    → Admin                  (thêm/sửa/xóa món, xem báo cáo)
//   ProcessOrders → Barista, Admin         (xem mọi đơn, đổi trạng thái, vào group barista)
//   PlaceOrders   → Customer               (đặt đơn; xem/hủy ĐƠN CỦA MÌNH — kiểm tra thêm trong controller)
// ============================================================================
using System.Security.Claims;
using CyberCafe.Contracts.Auth;

namespace CyberCafe.Api.Auth;

/// <summary>Tên policy dùng trong [Authorize(Policy = ...)].</summary>
public static class Policies
{
    /// <summary>Quản lý thực đơn + báo cáo.</summary>
    public const string ManageMenu = nameof(ManageMenu);

    /// <summary>Xử lý đơn tại quầy.</summary>
    public const string ProcessOrders = nameof(ProcessOrders);

    /// <summary>Khách đặt đơn.</summary>
    public const string PlaceOrders = nameof(PlaceOrders);

    // 👉 Bước 5 (b47.md)
    /// <summary>Đăng ký các policy (gọi trong Program.cs).</summary>
    public static IServiceCollection AddCyberCafePolicies(this IServiceCollection services) =>
        services.AddAuthorizationBuilder()
            .AddPolicy(ManageMenu, p => p.RequireRole(Roles.Admin))
            .AddPolicy(ProcessOrders, p => p.RequireRole(Roles.Barista, Roles.Admin)) // nhiều role = HOẶC
            .AddPolicy(PlaceOrders, p => p.RequireRole(Roles.Customer))
            .Services;
}

/// <summary>Đọc nhanh thông tin từ ClaimsPrincipal (User trong controller / Context.User trong hub).</summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>Id người dùng từ claim "sub"; null nếu chưa đăng nhập.</summary>
    public static int? GetUserId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(AppClaimTypes.UserId), out int id) ? id : null;

    /// <summary>Barista hoặc Admin — được xem/xử lý MỌI đơn.</summary>
    public static bool IsStaff(this ClaimsPrincipal user) =>
        user.IsInRole(Roles.Barista) || user.IsInRole(Roles.Admin);
}
