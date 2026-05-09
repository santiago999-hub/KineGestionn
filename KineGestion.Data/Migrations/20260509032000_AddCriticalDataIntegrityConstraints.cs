using KineGestion.Data.Context;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace KineGestion.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260509032000_AddCriticalDataIntegrityConstraints")]
    public partial class AddCriticalDataIntegrityConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Offices_Name",
                table: "Offices",
                column: "Name",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Patients_FechaNacimiento_Past",
                table: "Patients",
                sql: "[FechaNacimiento] < CONVERT(date, GETDATE())");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Patients_DNI_OnlyDigits",
                table: "Patients",
                sql: "[DNI] NOT LIKE '%[^0-9]%' AND LEN([DNI]) BETWEEN 7 AND 8");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Treatments_CantidadSesionesTotales_Positive",
                table: "Treatments",
                sql: "[CantidadSesionesTotales] >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sessions_Status_Valid",
                table: "Sessions",
                sql: "[Status] IN (0, 1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sessions_PaymentStatus_Valid",
                table: "Sessions",
                sql: "[PaymentStatus] IN (0, 1)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sessions_NroSesionEnTratamiento_Positive",
                table: "Sessions",
                sql: "[NroSesionEnTratamiento] >= 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Sessions_NroSesionEnTratamiento_Positive",
                table: "Sessions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Sessions_PaymentStatus_Valid",
                table: "Sessions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Sessions_Status_Valid",
                table: "Sessions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Treatments_CantidadSesionesTotales_Positive",
                table: "Treatments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Patients_DNI_OnlyDigits",
                table: "Patients");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Patients_FechaNacimiento_Past",
                table: "Patients");

            migrationBuilder.DropIndex(
                name: "IX_Offices_Name",
                table: "Offices");
        }
    }
}
