// ============================================================================
// InitialPayments — migration đầu tiên của Payment service, EF SINH TỰ ĐỘNG (Buổi 54 · database per service).
// Lệnh (project = startup = chính service này, khác Order service dùng -p Infrastructure -s Api):
//   dotnet ef migrations add InitialPayments -p src/CyberCafe.Payment.Api -s src/CyberCafe.Payment.Api -o Data/Migrations
// Tạo database CyberCafePayments (tách hẳn khỏi CyberCafeDb):
//   Payments          — giao dịch; UNIQUE(OrderId) chặn thu tiền 2 lần cho 1 đơn
//   ProcessedMessages — khóa ghép (MessageId, Consumer) cho idempotent consumer
// Lịch sử migration riêng (__EFMigrationsHistory trong database này) → 2 service đổi lược đồ độc lập.
// ============================================================================
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyberCafe.Payment.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    OrderCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Method = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CardLast4 = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    FailureReason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TransactionId = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RequestEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResultEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProcessedMessages",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Consumer = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedMessages", x => new { x.MessageId, x.Consumer });
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OrderId",
                table: "Payments",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ProcessedAtUtc",
                table: "Payments",
                column: "ProcessedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "ProcessedMessages");
        }
    }
}
