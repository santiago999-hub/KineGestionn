using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KineGestion.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCancellationReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Observaciones",
                table: "Sessions",
                type: "nvarchar(max)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "InternalNotes",
                table: "Sessions",
                type: "nvarchar(max)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Evolution",
                table: "Sessions",
                type: "nvarchar(max)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationObs",
                table: "Sessions",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CancellationReason",
                table: "Sessions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "Sessions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Telefono",
                table: "Patients",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Patients",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_CancellationReason_FechaHora",
                table: "Sessions",
                columns: new[] { "CancellationReason", "FechaHora" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sessions_CancellationReason_Valid",
                table: "Sessions",
                sql: "[CancellationReason] IS NULL OR [CancellationReason] IN (0, 1, 2, 3, 4, 5, 6, 7, 8)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sessions_CancellationReason_FechaHora",
                table: "Sessions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Sessions_CancellationReason_Valid",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "CancellationObs",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "Sessions");

            migrationBuilder.AlterColumn<string>(
                name: "Observaciones",
                table: "Sessions",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "InternalNotes",
                table: "Sessions",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Evolution",
                table: "Sessions",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Telefono",
                table: "Patients",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(512)",
                oldMaxLength: 512,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Patients",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(512)",
                oldMaxLength: 512,
                oldNullable: true);
        }
    }
}
