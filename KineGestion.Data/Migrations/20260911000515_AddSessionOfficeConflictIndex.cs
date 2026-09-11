using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KineGestion.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionOfficeConflictIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sessions_OfficeId",
                table: "Sessions");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_OfficeId_FechaHora",
                table: "Sessions",
                columns: new[] { "OfficeId", "FechaHora" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sessions_OfficeId_FechaHora",
                table: "Sessions");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_OfficeId",
                table: "Sessions",
                column: "OfficeId");
        }
    }
}
