using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.ATS
{
    /// <inheritdoc />
    public partial class ApplicationFormDataATSMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Resume",
                schema: "ats",
                table: "PersonalDetails",
                newName: "ResumeFileKey");

            migrationBuilder.RenameColumn(
                name: "NBIClearance",
                schema: "ats",
                table: "PersonalDetails",
                newName: "NBIClearanceFileKey");

            migrationBuilder.RenameColumn(
                name: "AdditionalGovtID",
                schema: "ats",
                table: "PersonalDetails",
                newName: "AdditionalGovtIDFileKey");

            migrationBuilder.RenameColumn(
                name: "SeniorHighSchoolDiploma",
                schema: "ats",
                table: "EducationalBackground",
                newName: "SeniorHighSchoolDiplomaFileKey");

            migrationBuilder.RenameColumn(
                name: "MastersDiploma",
                schema: "ats",
                table: "EducationalBackground",
                newName: "MastersDiplomaFileKey");

            migrationBuilder.RenameColumn(
                name: "HighSchoolDiploma",
                schema: "ats",
                table: "EducationalBackground",
                newName: "HighSchoolDiplomaFileKey");

            migrationBuilder.RenameColumn(
                name: "DoctorateDiploma",
                schema: "ats",
                table: "EducationalBackground",
                newName: "DoctorateDiplomaFileKey");

            migrationBuilder.RenameColumn(
                name: "CollegeDiploma",
                schema: "ats",
                table: "EducationalBackground",
                newName: "CollegeDiplomaFileKey");

            migrationBuilder.RenameColumn(
                name: "BachelorsDiploma",
                schema: "ats",
                table: "EducationalBackground",
                newName: "BachelorsDiplomaFileKey");

            migrationBuilder.AddColumn<string>(
                name: "PositionAppliedFor",
                schema: "ats",
                table: "PersonalDetails",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PositionAppliedFor",
                schema: "ats",
                table: "PersonalDetails");

            migrationBuilder.RenameColumn(
                name: "ResumeFileKey",
                schema: "ats",
                table: "PersonalDetails",
                newName: "Resume");

            migrationBuilder.RenameColumn(
                name: "NBIClearanceFileKey",
                schema: "ats",
                table: "PersonalDetails",
                newName: "NBIClearance");

            migrationBuilder.RenameColumn(
                name: "AdditionalGovtIDFileKey",
                schema: "ats",
                table: "PersonalDetails",
                newName: "AdditionalGovtID");

            migrationBuilder.RenameColumn(
                name: "SeniorHighSchoolDiplomaFileKey",
                schema: "ats",
                table: "EducationalBackground",
                newName: "SeniorHighSchoolDiploma");

            migrationBuilder.RenameColumn(
                name: "MastersDiplomaFileKey",
                schema: "ats",
                table: "EducationalBackground",
                newName: "MastersDiploma");

            migrationBuilder.RenameColumn(
                name: "HighSchoolDiplomaFileKey",
                schema: "ats",
                table: "EducationalBackground",
                newName: "HighSchoolDiploma");

            migrationBuilder.RenameColumn(
                name: "DoctorateDiplomaFileKey",
                schema: "ats",
                table: "EducationalBackground",
                newName: "DoctorateDiploma");

            migrationBuilder.RenameColumn(
                name: "CollegeDiplomaFileKey",
                schema: "ats",
                table: "EducationalBackground",
                newName: "CollegeDiploma");

            migrationBuilder.RenameColumn(
                name: "BachelorsDiplomaFileKey",
                schema: "ats",
                table: "EducationalBackground",
                newName: "BachelorsDiploma");
        }
    }
}
