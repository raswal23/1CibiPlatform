using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.ATS
{
    /// <inheritdoc />
    public partial class AddFileNameMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PhilSysImageKey",
                schema: "ats",
                table: "PersonalDetails",
                newName: "BiometricFileKey");

            migrationBuilder.AddColumn<string>(
                name: "SignatureFileName",
                schema: "ats",
                table: "SignatureDetails",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "COEUploadFileName",
                schema: "ats",
                table: "ProfessionalExperiences",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Emp1COEUploadFileName",
                schema: "ats",
                table: "ProfessionalExperiences",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Emp2COEUploadFileName",
                schema: "ats",
                table: "ProfessionalExperiences",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Emp3COEUploadFileName",
                schema: "ats",
                table: "ProfessionalExperiences",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdditionalGovtIDFileName",
                schema: "ats",
                table: "PersonalDetails",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BiometricFileName",
                schema: "ats",
                table: "PersonalDetails",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NBIClearanceFileName",
                schema: "ats",
                table: "PersonalDetails",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResumeFileName",
                schema: "ats",
                table: "PersonalDetails",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LicenseUploadFileName",
                schema: "ats",
                table: "LicensesDetails",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BachelorsDiplomaFileName",
                schema: "ats",
                table: "EducationalBackground",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CollegeDiplomaFileName",
                schema: "ats",
                table: "EducationalBackground",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DoctorateDiplomaFileName",
                schema: "ats",
                table: "EducationalBackground",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HighSchoolDiplomaFileName",
                schema: "ats",
                table: "EducationalBackground",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MastersDiplomaFileName",
                schema: "ats",
                table: "EducationalBackground",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SeniorHighSchoolDiplomaFileName",
                schema: "ats",
                table: "EducationalBackground",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SignatureFileName",
                schema: "ats",
                table: "SignatureDetails");

            migrationBuilder.DropColumn(
                name: "COEUploadFileName",
                schema: "ats",
                table: "ProfessionalExperiences");

            migrationBuilder.DropColumn(
                name: "Emp1COEUploadFileName",
                schema: "ats",
                table: "ProfessionalExperiences");

            migrationBuilder.DropColumn(
                name: "Emp2COEUploadFileName",
                schema: "ats",
                table: "ProfessionalExperiences");

            migrationBuilder.DropColumn(
                name: "Emp3COEUploadFileName",
                schema: "ats",
                table: "ProfessionalExperiences");

            migrationBuilder.DropColumn(
                name: "AdditionalGovtIDFileName",
                schema: "ats",
                table: "PersonalDetails");

            migrationBuilder.DropColumn(
                name: "BiometricFileName",
                schema: "ats",
                table: "PersonalDetails");

            migrationBuilder.DropColumn(
                name: "NBIClearanceFileName",
                schema: "ats",
                table: "PersonalDetails");

            migrationBuilder.DropColumn(
                name: "ResumeFileName",
                schema: "ats",
                table: "PersonalDetails");

            migrationBuilder.DropColumn(
                name: "LicenseUploadFileName",
                schema: "ats",
                table: "LicensesDetails");

            migrationBuilder.DropColumn(
                name: "BachelorsDiplomaFileName",
                schema: "ats",
                table: "EducationalBackground");

            migrationBuilder.DropColumn(
                name: "CollegeDiplomaFileName",
                schema: "ats",
                table: "EducationalBackground");

            migrationBuilder.DropColumn(
                name: "DoctorateDiplomaFileName",
                schema: "ats",
                table: "EducationalBackground");

            migrationBuilder.DropColumn(
                name: "HighSchoolDiplomaFileName",
                schema: "ats",
                table: "EducationalBackground");

            migrationBuilder.DropColumn(
                name: "MastersDiplomaFileName",
                schema: "ats",
                table: "EducationalBackground");

            migrationBuilder.DropColumn(
                name: "SeniorHighSchoolDiplomaFileName",
                schema: "ats",
                table: "EducationalBackground");

            migrationBuilder.RenameColumn(
                name: "BiometricFileKey",
                schema: "ats",
                table: "PersonalDetails",
                newName: "PhilSysImageKey");
        }
    }
}
