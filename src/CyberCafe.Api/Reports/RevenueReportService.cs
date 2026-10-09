// ============================================================================
// RevenueReportService.cs — báo cáo doanh thu theo ngày (Buổi 36–41 · stored procedure + LINQ GroupBy).
// Hai cách cho CÙNG 1 kết quả, chọn theo database provider:
//   (A) SQL Server  → gọi stored procedure dbo.usp_DailyRevenue (tạo trong migration AddDailyRevenueProcedure)
//                     bằng Database.SqlQuery<T>($"...") — THAM SỐ HÓA, an toàn SQL injection.
//   (B) Provider khác (EF InMemory trong test) → LINQ GroupBy. InMemory không chạy được SQL thô.
// Vì sao vẫn giữ SP khi LINQ làm được? Để học cách gọi SP từ EF: thực tế DBA hay viết sẵn SP báo cáo
// (tối ưu, phân quyền EXECUTE riêng) và ứng dụng chỉ việc gọi.
// ============================================================================
using CyberCafe.Api.Data;
using CyberCafe.Contracts.Reports;
using CyberCafe.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace CyberCafe.Api.Reports;

/// <summary>Báo cáo doanh thu (không tính đơn đã hủy). Doanh thu = tổng Payments.Amount đã thu.</summary>
public class RevenueReportService(CyberCafeDbContext db)
{
    /// <summary>Doanh thu từng ngày trong khoảng [from, to] (tính cả 2 đầu).</summary>
    public async Task<IReadOnlyList<DailyRevenueDto>> GetDailyRevenueAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        // 👉 Bước 8 (b40.md): kiểm tra provider. IsSqlServer() là extension của gói EF SqlServer.
        if (db.Database.IsSqlServer())
        {
            // {from}, {to} trong chuỗi $"..." KHÔNG bị nối chuỗi: SqlQuery nhận FormattableString
            // và biến từng {…} thành tham số @p0, @p1 → SQL Server nhận giá trị riêng, không "chạy" được.
            // ⚠️ Lỗi hay gặp (SQL injection): SqlQueryRaw("EXEC ... '" + from + "'") — nối chuỗi tay.
            // EXEC không "ghép" thêm được (non-composable) → gọi ToListAsync ngay, không Where/OrderBy thêm.
            return await db.Database
                .SqlQuery<DailyRevenueDto>($"EXEC dbo.usp_DailyRevenue @From = {from}, @To = {to}")
                .ToListAsync(ct);
        }

        // (B) LINQ: lọc → nhóm theo ngày → tổng. Với SQL Server câu này cũng dịch được sang GROUP BY.
        DateTime start = from.ToDateTime(TimeOnly.MinValue);
        DateTime endExclusive = to.AddDays(1).ToDateTime(TimeOnly.MinValue); // "< ngày hôm sau" thay vì "<= 23:59:59"
        var rows = await db.Orders
            .AsNoTracking()
            .Where(o => o.Status != OrderStatus.Cancelled && o.CreatedAt >= start && o.CreatedAt < endExclusive && o.Payment != null)
            .GroupBy(o => o.CreatedAt.Date)
            .Select(g => new { Day = g.Key, OrderCount = g.Count(), Revenue = g.Sum(o => o.Payment!.Amount) })
            .OrderBy(x => x.Day)
            .ToListAsync(ct);

        return rows.Select(x => new DailyRevenueDto(DateOnly.FromDateTime(x.Day), x.OrderCount, x.Revenue)).ToList();
    }
}
