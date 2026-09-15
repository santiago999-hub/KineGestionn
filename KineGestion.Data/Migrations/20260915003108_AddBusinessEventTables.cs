using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KineGestion.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessEventTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BillingBatchEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Operation = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RequestedCount = table.Column<int>(type: "int", nullable: false),
                    UpdatedCount = table.Column<int>(type: "int", nullable: false),
                    SkippedCount = table.Column<int>(type: "int", nullable: false),
                    FilterDateFrom = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FilterDateTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FilterSearch = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OnlyCompletedPending = table.Column<bool>(type: "bit", nullable: true),
                    ChangedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingBatchEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DispatchEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DispatchType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SessionId = table.Column<int>(type: "int", nullable: true),
                    ChangedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EmailSent = table.Column<bool>(type: "bit", nullable: false),
                    WhatsAppSent = table.Column<bool>(type: "bit", nullable: false),
                    Errors = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DispatchEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BillingBatchEvents_CreatedAtUtc",
                table: "BillingBatchEvents",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_DispatchEvents_DispatchType_SentAtUtc",
                table: "DispatchEvents",
                columns: new[] { "DispatchType", "SentAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DispatchEvents_SessionId",
                table: "DispatchEvents",
                column: "SessionId");

            migrationBuilder.Sql("""
                INSERT INTO [BillingBatchEvents]
                    ([Operation], [RequestedCount], [UpdatedCount], [SkippedCount],
                     [FilterDateFrom], [FilterDateTo], [FilterSearch], [OnlyCompletedPending],
                     [ChangedBy], [CreatedAtUtc])
                SELECT
                    ISNULL(JSON_VALUE(a.[NewValuesJson], '$.Operation'), 'MarkPaidBatch'),
                    ISNULL(TRY_CAST(JSON_VALUE(a.[NewValuesJson], '$.RequestedCount') AS int), 0),
                    ISNULL(TRY_CAST(JSON_VALUE(a.[NewValuesJson], '$.UpdatedCount') AS int), 0),
                    ISNULL(TRY_CAST(JSON_VALUE(a.[NewValuesJson], '$.SkippedCount') AS int), 0),
                    TRY_CAST(JSON_VALUE(a.[NewValuesJson], '$.Filters.DateFrom') AS datetime2),
                    TRY_CAST(JSON_VALUE(a.[NewValuesJson], '$.Filters.DateTo') AS datetime2),
                    CASE WHEN LEN(JSON_VALUE(a.[NewValuesJson], '$.Filters.Search')) > 200
                         THEN LEFT(JSON_VALUE(a.[NewValuesJson], '$.Filters.Search'), 200)
                         ELSE JSON_VALUE(a.[NewValuesJson], '$.Filters.Search') END,
                    TRY_CAST(JSON_VALUE(a.[NewValuesJson], '$.Filters.OnlyCompletedPending') AS bit),
                    a.[ChangedBy],
                    a.[ChangedAt]
                FROM [AuditLogs] a
                WHERE a.[EntityName] = 'BillingBatch' AND a.[Action] = 'Create';
                """);

            migrationBuilder.Sql("""
                INSERT INTO [DispatchEvents]
                    ([DispatchType], [SessionId], [ChangedBy], [SentAtUtc], [EmailSent], [WhatsAppSent], [Errors])
                SELECT
                    CASE a.[EntityName]
                        WHEN 'ReminderDispatch' THEN 'PatientReminder'
                        WHEN 'OperationalAlert' THEN 'BillingBatchLowEffectivenessAlert'
                        ELSE COALESCE(JSON_VALUE(a.[NewValuesJson], '$.DispatchType'), 'BillingFollowUp')
                    END,
                    CASE WHEN a.[EntityName] = 'OperationalAlert' THEN NULL ELSE TRY_CAST(a.[EntityId] AS int) END,
                    a.[ChangedBy],
                    a.[ChangedAt],
                    ISNULL(TRY_CAST(JSON_VALUE(a.[NewValuesJson], '$.EmailSent') AS bit), 0),
                    ISNULL(TRY_CAST(JSON_VALUE(a.[NewValuesJson], '$.WhatsAppSent') AS bit), 0),
                    CASE
                        WHEN JSON_VALUE(a.[NewValuesJson], '$.Errors[0]') IS NULL THEN NULL
                        ELSE LEFT(
                            LTRIM(RTRIM(JSON_VALUE(a.[NewValuesJson], '$.Errors[0]')))
                            + CASE WHEN JSON_VALUE(a.[NewValuesJson], '$.Errors[1]') IS NULL THEN '' ELSE ' | ' + JSON_VALUE(a.[NewValuesJson], '$.Errors[1]') END,
                            2000)
                    END
                FROM [AuditLogs] a
                WHERE a.[EntityName] IN ('ReminderDispatch', 'BillingFollowUp', 'OperationalAlert')
                  AND a.[Action] = 'Create';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BillingBatchEvents");

            migrationBuilder.DropTable(
                name: "DispatchEvents");
        }
    }
}
