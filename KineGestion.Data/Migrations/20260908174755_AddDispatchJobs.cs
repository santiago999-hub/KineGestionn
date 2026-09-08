using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KineGestion.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDispatchJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DispatchJobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<int>(type: "int", nullable: false),
                    DispatchType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PayloadHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    ClaimToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClaimedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DispatchJobs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DispatchJobs_CreatedAtUtc",
                table: "DispatchJobs",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_DispatchJobs_SessionId_DispatchType_PayloadHash",
                table: "DispatchJobs",
                columns: new[] { "SessionId", "DispatchType", "PayloadHash" },
                unique: true,
                filter: "[Status] IN (0, 1)");

            migrationBuilder.CreateIndex(
                name: "IX_DispatchJobs_Status_NextAttemptAtUtc_CreatedAtUtc",
                table: "DispatchJobs",
                columns: new[] { "Status", "NextAttemptAtUtc", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DispatchJobs");
        }
    }
}
