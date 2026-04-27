using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KineGestion.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTreatmentPatientRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PatientId",
                table: "Treatments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Treatments_PatientId",
                table: "Treatments",
                column: "PatientId");

            migrationBuilder.AddForeignKey(
                name: "FK_Treatments_Patients_PatientId",
                table: "Treatments",
                column: "PatientId",
                principalTable: "Patients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Treatments_Patients_PatientId",
                table: "Treatments");

            migrationBuilder.DropIndex(
                name: "IX_Treatments_PatientId",
                table: "Treatments");

            migrationBuilder.DropColumn(
                name: "PatientId",
                table: "Treatments");
        }
    }
}
