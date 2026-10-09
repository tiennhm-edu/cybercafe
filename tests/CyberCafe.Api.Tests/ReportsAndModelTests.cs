// ============================================================================
// ReportsAndModelTests.cs — báo cáo doanh thu + kiểm tra migration (Buổi 36–41).
// - Báo cáo: InMemory KHÔNG chạy được stored procedure → RevenueReportService tự chọn nhánh LINQ
//   (kiểm tra provider bằng Database.IsSqlServer()). Nhánh SP được kiểm thử tay với Docker (xem b40.md).
// - Migration: model hiện tại phải khớp snapshot của migration cuối — quên "dotnet ef migrations add"
//   sau khi sửa Configuration là test này đỏ (không cần kết nối SQL Server).
// ============================================================================
using System.Net;
using System.Net.Http.Json;
using CyberCafe.Api.Data;
using CyberCafe.Contracts.Orders;
using CyberCafe.Contracts.Reports;
using Microsoft.EntityFrameworkCore;

namespace CyberCafe.Api.Tests;

public class ReportsAndModelTests
{
    // Kiểm tra: doanh thu hôm nay = tổng tiền các đơn KHÔNG bị hủy (nhánh LINQ GroupBy trên InMemory).
    [Fact]
    public async Task DailyRevenue_ExcludesCancelledOrders()
    {
        await using CyberCafeApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        OrderDto kept = await client.PlaceAsync();
        OrderDto cancelled = await client.PlaceAsync();
        await client.PostAsync($"/api/orders/{cancelled.Id}/cancel", null);
        string today = DateTime.Now.ToString("yyyy-MM-dd");

        List<DailyRevenueDto> rows = (await client.GetFromJsonAsync<List<DailyRevenueDto>>(
            $"/api/reports/daily-revenue?from={today}&to={today}", CyberCafeApiFactory.Json))!;

        DailyRevenueDto row = Assert.Single(rows);
        Assert.Equal(1, row.OrderCount);
        Assert.Equal(kept.FinalAmount, row.Revenue);
    }

    // Kiểm tra: from > to → 400.
    [Fact]
    public async Task DailyRevenue_InvalidRange_Returns400()
    {
        await using CyberCafeApiFactory factory = new();

        HttpResponseMessage response = await factory.CreateClient().GetAsync("/api/reports/daily-revenue?from=2026-10-09&to=2026-10-01");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Kiểm tra: không còn thay đổi model nào chưa có migration, và migration SP có trong danh sách.
    [Fact]
    public void Migrations_AreUpToDate()
    {
        // UseSqlServer với chuỗi kết nối giả: chỉ dựng MODEL SQL Server, không mở kết nối thật.
        DbContextOptions<CyberCafeDbContext> options = new DbContextOptionsBuilder<CyberCafeDbContext>()
            .UseSqlServer("Server=(local);Database=ModelOnly;Trusted_Connection=True")
            .Options;
        using CyberCafeDbContext db = new(options);

        Assert.False(db.Database.HasPendingModelChanges(), "Có thay đổi model chưa tạo migration: chạy dotnet ef migrations add ...");
        Assert.Contains(db.Database.GetMigrations(), m => m.EndsWith("_AddDailyRevenueProcedure", StringComparison.Ordinal));
    }

    // Kiểm tra: seed thực đơn (trước là MenuService buổi 23) đủ 3 nhóm, giá dương, Id không trùng.
    [Fact]
    public void MenuSeed_HasThreeCategories_PositivePrices_UniqueIds()
    {
        var products = MenuSeed.Products();

        Assert.Equal(["Cà phê", "Trà", "Bánh"], products.Select(p => p.Category).Distinct().ToArray());
        Assert.All(products, p => Assert.True(p.Price > 0));
        Assert.Equal(products.Count, products.Select(p => p.Id).Distinct().Count());
    }
}
