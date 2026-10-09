// ============================================================================
// IRevenueReportService.cs — cổng (port) báo cáo doanh thu (Buổi 36–41 · 48).
// Cài đặt (Infrastructure/Reports/RevenueReportService.cs) gọi stored procedure trên SQL Server hoặc
// LINQ GroupBy trên provider khác — chi tiết đó là của HẠ TẦNG, ReportsController không cần biết.
// ============================================================================
using CyberCafe.Contracts.Reports;

namespace CyberCafe.Application.Reports;

/// <summary>Báo cáo doanh thu (không tính đơn đã hủy).</summary>
public interface IRevenueReportService
{
    /// <summary>Doanh thu từng ngày trong khoảng [from, to] (tính cả 2 đầu).</summary>
    Task<IReadOnlyList<DailyRevenueDto>> GetDailyRevenueAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
}
