using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KineGestion.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Sessions_ProfessionalId_FechaHora",
                table: "Sessions",
                columns: new[] { "ProfessionalId", "FechaHora" });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_Status_FechaHora",
                table: "Sessions",
                columns: new[] { "Status", "FechaHora" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sessions_ProfessionalId_FechaHora",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_Status_FechaHora",
                table: "Sessions");
        }
    }
}
