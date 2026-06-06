using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.ATS
{
    /// <inheritdoc />
    public partial class ApplicationFormDataVersion5ATSMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Emp1COEUploadFileKey",
                schema: "ats",
                table: "ProfessionalExperiences",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Emp2COEUploadFileKey",
                schema: "ats",
                table: "ProfessionalExperiences",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Emp3COEUploadFileKey",
                schema: "ats",
                table: "ProfessionalExperiences",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Emp1COEUploadFileKey",
                schema: "ats",
                table: "ProfessionalExperiences");

            migrationBuilder.DropColumn(
                name: "Emp2COEUploadFileKey",
                schema: "ats",
                table: "ProfessionalExperiences");

            migrationBuilder.DropColumn(
                name: "Emp3COEUploadFileKey",
                schema: "ats",
                table: "ProfessionalExperiences");
        }
    }
}
