using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KineGestion.Data.Migrations
{
    /// <inheritdoc />
    public partial class UniqueIndex_Sessions_TreatmentId_NroSesion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Sessions_TreatmentId_NroSesionEnTratamiento",
                table: "Sessions",
                columns: new[] { "TreatmentId", "NroSesionEnTratamiento" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sessions_TreatmentId_NroSesionEnTratamiento",
                table: "Sessions");
        }
    }
}
