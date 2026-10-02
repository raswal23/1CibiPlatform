using ATS.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.ATS
{
	/// <summary>
	/// Removes the five <c>College*</c> columns from <c>EducationalBackground</c> and the two
	/// un-prefixed <c>COEUploadFile*</c> columns from <c>ProfessionalExperiences</c>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Hand-written (the EF CLI could not run in this environment); the attributes below carry
	/// what the Designer file would normally declare. Without them EF cannot discover the
	/// migration at all, <c>Database.MigrateAsync()</c> silently skips it, and the columns stay
	/// behind against a green build. The model snapshot is updated in the same commit. Same
	/// approach as DropUnusedAtsTablesAndColumns and DropReasonForLeavingFromProfessionalExperiences.
	/// </para>
	/// <para>
	/// The <c>College*</c> tier duplicated <c>Bachelors*</c>. "College Graduate" is a label in the
	/// form's attainment dropdown, but the Blazor component writes that answer to the
	/// <c>Bachelors*</c> fields on the submit DTO, and <c>ProfessionalExperiencesDTO</c> has no
	/// College members at all - so the College columns were only ever filled by
	/// <c>ApplicationFormService</c> copying the Bachelors values into them immediately before
	/// the insert. Every read then coalesced <c>Bachelors ?? College</c>, so the copy could never
	/// be reached. <c>CollegeDiplomaFileName</c> was not even part of that copy, leaving it null
	/// while <c>CollegeDiplomaFileKey</c> held a real object key. The duplication is removed
	/// rather than repaired: Bachelors is the single home for this tier.
	/// </para>
	/// <para>
	/// The un-prefixed <c>COEUploadFileKey</c>/<c>COEUploadFileName</c> predate per-employer COEs.
	/// Only <c>Emp1</c>/<c>Emp2</c>/<c>Emp3</c> variants are written now; the singular pair had no
	/// writer anywhere in the solution and survived only as trailing <c>??</c> arms and one extra
	/// <c>AtsDocumentTypes.Coe</c> entry in the document compiler. Those readers are removed with
	/// the columns. The <c>AtsDocumentTypes.Coe</c> constant itself stays - ReportService and the
	/// download picker still use it for the per-employer COEs.
	/// </para>
	/// <para>
	/// Down re-creates the columns but NOT their contents. Note the two <c>*FileName</c> columns
	/// come back as <c>text</c>, not <c>character varying(255)</c>: neither had a
	/// <c>HasMaxLength</c> in its entity configuration, so that is the width the model actually
	/// had when they were dropped, and a down-then-up round trip lands where it started.
	/// </para>
	/// </remarks>
	[DbContext(typeof(ATSDBContext))]
	[Migration("20260923100000_DropCollegeTierAndLegacyCoeColumns")]
	public partial class DropCollegeTierAndLegacyCoeColumns : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropColumn(
				name: "CollegeSchoolName",
				schema: "ats",
				table: "EducationalBackground");

			migrationBuilder.DropColumn(
				name: "CollegeGraduationDate",
				schema: "ats",
				table: "EducationalBackground");

			migrationBuilder.DropColumn(
				name: "CollegeDiplomaFileKey",
				schema: "ats",
				table: "EducationalBackground");

			migrationBuilder.DropColumn(
				name: "CollegeDiplomaFileName",
				schema: "ats",
				table: "EducationalBackground");

			migrationBuilder.DropColumn(
				name: "CollegeDegree",
				schema: "ats",
				table: "EducationalBackground");

			migrationBuilder.DropColumn(
				name: "COEUploadFileKey",
				schema: "ats",
				table: "ProfessionalExperiences");

			migrationBuilder.DropColumn(
				name: "COEUploadFileName",
				schema: "ats",
				table: "ProfessionalExperiences");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<string>(
				name: "COEUploadFileName",
				schema: "ats",
				table: "ProfessionalExperiences",
				type: "text",
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "COEUploadFileKey",
				schema: "ats",
				table: "ProfessionalExperiences",
				type: "character varying(255)",
				maxLength: 255,
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "CollegeDegree",
				schema: "ats",
				table: "EducationalBackground",
				type: "character varying(255)",
				maxLength: 255,
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "CollegeDiplomaFileName",
				schema: "ats",
				table: "EducationalBackground",
				type: "text",
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "CollegeDiplomaFileKey",
				schema: "ats",
				table: "EducationalBackground",
				type: "character varying(255)",
				maxLength: 255,
				nullable: true);

			migrationBuilder.AddColumn<DateOnly>(
				name: "CollegeGraduationDate",
				schema: "ats",
				table: "EducationalBackground",
				type: "date",
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "CollegeSchoolName",
				schema: "ats",
				table: "EducationalBackground",
				type: "character varying(255)",
				maxLength: 255,
				nullable: true);
		}
	}
}
