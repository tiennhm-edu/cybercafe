// ============================================================================
// ReportsController.cs — báo cáo /api/reports (Buổi 36–41 · stored procedure; Buổi 42–47 · chỉ Admin; Buổi 48).
// Controller mỏng: validate tham số rồi giao cho IRevenueReportService (Application; cài đặt ở Infrastructure).
// ============================================================================
using CyberCafe.Api.Auth;
using CyberCafe.Application.Reports;
using CyberCafe.Contracts.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CyberCafe.Api.Controllers;

/// <summary>Báo cáo kinh doanh.</summary>
[ApiController]
[Route("api/reports")]
[Authorize(Policy = Policies.ManageMenu)] // doanh thu là dữ liệu nhạy cảm: chỉ Admin
public class ReportsController(IRevenueReportService reports) : ControllerBase
{
    /// <summary>Doanh thu theo ngày trong khoảng [from, to] (tối đa 366 ngày). Mặc định: 7 ngày gần nhất.</summary>
    /// <remarks>Ví dụ: GET /api/reports/daily-revenue?from=2026-10-01&amp;to=2026-10-09</remarks>
    [HttpGet("daily-revenue")]
    [ProducesResponseType<IReadOnlyList<DailyRevenueDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<DailyRevenueDto>>> DailyRevenue(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct)
    {
        DateOnly end = to ?? DateOnly.FromDateTime(DateTime.Now);
        DateOnly start = from ?? end.AddDays(-6);

        if (start > end || end.DayNumber - start.DayNumber > 366)
        {
            this.ModelState.AddModelError(nameof(from), "Khoảng ngày không hợp lệ (from ≤ to, tối đa 366 ngày)");
            return this.ValidationProblem(this.ModelState);
        }

        return this.Ok(await reports.GetDailyRevenueAsync(start, end, ct));
    }
}
