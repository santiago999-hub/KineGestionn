using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KineGestion.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRound3QueryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Treatments_PatientId",
                table: "Treatments");

            migrationBuilder.CreateIndex(
                name: "IX_Treatments_PatientId_FechaInicio",
                table: "Treatments",
                columns: new[] { "PatientId", "FechaInicio" });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_PaymentStatus_FechaHora",
                table: "Sessions",
                columns: new[] { "PaymentStatus", "FechaHora" });

            migrationBuilder.CreateIndex(
                name: "IX_Professionals_IsActivo_Apellido_Nombre",
                table: "Professionals",
                columns: new[] { "IsActivo", "Apellido", "Nombre" });

            migrationBuilder.CreateIndex(
                name: "IX_Patients_IsActivo_Apellido_Nombre",
                table: "Patients",
                columns: new[] { "IsActivo", "Apellido", "Nombre" });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_Email",
                table: "AspNetUsers",
                column: "Email");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Treatments_PatientId_FechaInicio",
                table: "Treatments");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_PaymentStatus_FechaHora",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_Professionals_IsActivo_Apellido_Nombre",
                table: "Professionals");

            migrationBuilder.DropIndex(
                name: "IX_Patients_IsActivo_Apellido_Nombre",
                table: "Patients");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_Email",
                table: "AspNetUsers");

            migrationBuilder.CreateIndex(
                name: "IX_Treatments_PatientId",
                table: "Treatments",
                column: "PatientId");
        }
    }
}
