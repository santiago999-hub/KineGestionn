using KineGestion.Data.Context;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KineGestion.Data.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260509043000_AddAuditUserColumns")]
    public partial class AddAuditUserColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Equipments",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "system");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "Equipments",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "system");

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Offices",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "system");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "Offices",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "system");

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Patients",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "system");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "Patients",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "system");

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Professionals",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "system");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "Professionals",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "system");

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Sessions",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "system");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "Sessions",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "system");

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Treatments",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "system");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "Treatments",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "system");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "CreatedBy", table: "Equipments");
            migrationBuilder.DropColumn(name: "UpdatedBy", table: "Equipments");
            migrationBuilder.DropColumn(name: "CreatedBy", table: "Offices");
            migrationBuilder.DropColumn(name: "UpdatedBy", table: "Offices");
            migrationBuilder.DropColumn(name: "CreatedBy", table: "Patients");
            migrationBuilder.DropColumn(name: "UpdatedBy", table: "Patients");
            migrationBuilder.DropColumn(name: "CreatedBy", table: "Professionals");
            migrationBuilder.DropColumn(name: "UpdatedBy", table: "Professionals");
            migrationBuilder.DropColumn(name: "CreatedBy", table: "Sessions");
            migrationBuilder.DropColumn(name: "UpdatedBy", table: "Sessions");
            migrationBuilder.DropColumn(name: "CreatedBy", table: "Treatments");
            migrationBuilder.DropColumn(name: "UpdatedBy", table: "Treatments");
        }
    }
}