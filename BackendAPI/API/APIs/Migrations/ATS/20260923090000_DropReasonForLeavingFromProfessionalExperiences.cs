using ATS.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.ATS
{
	/// <summary>
	/// Removes <c>Emp1ReasonForLeaving</c>, <c>Emp2ReasonForLeaving</c> and
	/// <c>Emp3ReasonForLeaving</c> from <c>ProfessionalExperiences</c> - three columns no
	/// application code writes.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Hand-written (the EF CLI could not run in this environment); the attributes below carry
	/// what the Designer file would normally declare. Without them EF cannot discover the
	/// migration at all, <c>Database.MigrateAsync()</c> silently skips it, and the columns stay
	/// behind against a green build. The model snapshot is updated in the same commit. Same
	/// approach as DropUnusedAtsTablesAndColumns, which removed thirteen columns this way.
	/// </para>
	/// <para>
	/// These columns date from the initial migration but the question was never added to the
	/// application form: "reason for leaving" is absent from <c>ProfessionalExperiencesDTO</c>
	/// on both the API and the Blazor side, and from step 5 of ApplicationFormComponent, so no
	/// submission could ever set it. The read side was wired - the report projection, both
	/// preview DTOs, the preview dialog and the PDF all carried the field - but with nothing
	/// populating it, every candidate record held null and the renderers, which skip blank
	/// values, never showed it. Those readers are removed alongside the columns.
	/// </para>
	/// <para>
	/// The only values these columns can hold come from the Intouch seed data, whose generator
	/// synthesised one per employer because the property existed on the entity; that generator
	/// rule and the seeded values are removed in the same commit.
	/// </para>
	/// <para>
	/// Down re-creates the columns but NOT their contents; any values are gone at Up. That is
	/// acceptable precisely because no shipped write path produced them.
	/// </para>
	/// </remarks>
	[DbContext(typeof(ATSDBContext))]
	[Migration("20260923090000_DropReasonForLeavingFromProfessionalExperiences")]
	public partial class DropReasonForLeavingFromProfessionalExperiences : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropColumn(
				name: "Emp1ReasonForLeaving",
				schema: "ats",
				table: "ProfessionalExperiences");

			migrationBuilder.DropColumn(
				name: "Emp2ReasonForLeaving",
				schema: "ats",
				table: "ProfessionalExperiences");

			migrationBuilder.DropColumn(
				name: "Emp3ReasonForLeaving",
				schema: "ats",
				table: "ProfessionalExperiences");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			// Re-created at the width the model had when they were dropped - 255, set by
			// IncreasedMaxLengthToAllEntityATSMigration - rather than the 100 the initial
			// migration used, so a down-then-up round trip lands where it started.
			migrationBuilder.AddColumn<string>(
				name: "Emp3ReasonForLeaving",
				schema: "ats",
				table: "ProfessionalExperiences",
				type: "character varying(255)",
				maxLength: 255,
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "Emp2ReasonForLeaving",
				schema: "ats",
				table: "ProfessionalExperiences",
				type: "character varying(255)",
				maxLength: 255,
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "Emp1ReasonForLeaving",
				schema: "ats",
				table: "ProfessionalExperiences",
				type: "character varying(255)",
				maxLength: 255,
				nullable: true);
		}
	}
}
