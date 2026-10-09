// ============================================================================
// AddOutboxAndInbox — migration thứ 4, EF SINH TỰ ĐỘNG (Buổi 55 · Outbox + idempotent consumer).
// Lệnh: dotnet ef migrations add AddOutboxAndInbox -p src/CyberCafe.Infrastructure -s src/CyberCafe.Api -o Persistence/Migrations
// Thêm 2 bảng, KHÔNG đổi bảng cũ nào (Orders/Payments giữ nguyên → dữ liệu b40–b53 dùng tiếp):
//   OutboxMessages    — integration event chờ gửi (khóa = EventId; index ProcessedAtUtc + OccurredAtUtc cho dispatcher)
//   ProcessedMessages — message đã xử lý (khóa ghép MessageId + Consumer → DB tự chặn xử lý 2 lần)
// Chạy dưới AppHost: Api tự Migrate() lúc khởi động (Database:MigrateOnStartup = true do AppHost đặt).
// Chạy kiểu cũ (docker compose): dotnet ef database update -p src/CyberCafe.Infrastructure -s src/CyberCafe.Api
// ============================================================================
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyberCafe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxAndInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TraceParent = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
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
                name: "IX_OutboxMessages_ProcessedAtUtc_OccurredAtUtc",
                table: "OutboxMessages",
                columns: new[] { "ProcessedAtUtc", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OutboxMessages");

            migrationBuilder.DropTable(
                name: "ProcessedMessages");
        }
    }
}
