// ============================================================================
// DailyRevenueDto.cs — 1 dòng báo cáo doanh thu theo ngày (Buổi 36–41 · stored procedure).
// Tên property PHẢI trùng tên cột mà usp_DailyRevenue trả về (Day, OrderCount, Revenue):
// Database.SqlQuery<T> map cột → property theo TÊN.
// ============================================================================
namespace CyberCafe.Contracts.Reports;

/// <summary>Doanh thu 1 ngày (không tính đơn đã hủy).</summary>
/// <param name="Day">Ngày (theo giờ máy chủ API).</param>
/// <param name="OrderCount">Số đơn trong ngày.</param>
/// <param name="Revenue">Tổng tiền đã thu (VND).</param>
public record DailyRevenueDto(DateOnly Day, int OrderCount, decimal Revenue);
