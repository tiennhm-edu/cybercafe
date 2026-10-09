// ============================================================================
// AddDailyRevenueProcedure — migration VIẾT TAY (Buổi 36–41 · 48 · stored procedure trong migration).
// Tạo bằng: dotnet ef migrations add AddDailyRevenueProcedure -p src/CyberCafe.Api -o Data/Migrations
// EF chỉ sinh khung Up/Down rỗng (model không đổi) → ta tự viết SQL bằng migrationBuilder.Sql(...).
// Nhờ nằm trong migration, SP được tạo/xóa cùng lúc với schema trên MỌI máy (dev, CI, production),
// thay vì "nhớ chạy tay file .sql".
// Buổi 48 (Clean Architecture): cả thư mục Migrations chuyển từ Api/Data sang Infrastructure/Persistence.
//   Chỉ đổi namespace; Id migration (tên file + [Migration("...")]) GIỮ NGUYÊN → DB đang chạy b47 không phải làm gì.
//   Lệnh mới: dotnet ef migrations add <Tên> -p src/CyberCafe.Infrastructure -s src/CyberCafe.Api -o Persistence/Migrations
// ============================================================================
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyberCafe.Infrastructure.Persistence.Migrations
{
    /// <summary>Tạo stored procedure dbo.usp_DailyRevenue (doanh thu theo ngày).</summary>
    public partial class AddDailyRevenueProcedure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 👉 Bước 8 (b40.md)
            // Doanh thu = tổng tiền đã thu (Payments.Amount) của các đơn KHÔNG bị hủy, nhóm theo ngày.
            // Tên cột trả về (Day, OrderCount, Revenue) phải TRÙNG tên property của DailyRevenueDto.
            // ⚠️ Lỗi hay gặp: đặt tên "sp_..." → SQL Server tìm trong database master trước (chậm hơn,
            //    dễ trùng SP hệ thống). Quy ước dùng tiền tố "usp_" (user stored procedure).
            // CREATE OR ALTER (SQL Server 2016 SP1+): chạy lại migration không lỗi "đã tồn tại".
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.usp_DailyRevenue
                    @From date,
                    @To   date
                AS
                BEGIN
                    SET NOCOUNT ON;  -- không gửi thông điệp "n rows affected" thừa về client

                    SELECT CAST(o.CreatedAt AS date) AS [Day],
                           COUNT(*)                  AS OrderCount,
                           SUM(p.Amount)             AS Revenue
                    FROM dbo.Orders AS o
                    INNER JOIN dbo.Payments AS p ON p.Id = o.PaymentId
                    WHERE o.Status <> N'Cancelled'
                      -- ">= @From AND < ngày sau @To": dùng được index IX_Orders_CreatedAt (sargable),
                      -- khác với WHERE CAST(o.CreatedAt AS date) BETWEEN ... (phải tính trên từng dòng)
                      AND o.CreatedAt >= @From
                      AND o.CreatedAt <  DATEADD(day, 1, @To)
                    GROUP BY CAST(o.CreatedAt AS date)
                    ORDER BY [Day];
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Down phải "hoàn tác" đúng những gì Up đã làm → dotnet ef database update InitialCreate quay lui được.
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_DailyRevenue;");
        }
    }
}
