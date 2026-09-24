using System;
using ATS.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.ATS
{
    /// <summary>
    /// Removes the <c>DocumentDetails</c> table and thirteen columns on
    /// <c>EducationalBackground</c> and <c>AddressDetails</c> that no code reads or writes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hand-written (the EF CLI could not run in this environment); the attributes below carry
    /// what the Designer file would normally declare. Without them EF cannot discover the
    /// migration at all, <c>Database.MigrateAsync()</c> silently skips it, and the columns stay
    /// behind against a green build. The model snapshot is updated in the same commit.
    /// </para>
    /// <para>
    /// <c>DocumentDetails</c> was created by the initial migration and never wired to anything:
    /// no service, repository, DTO, endpoint or component referenced the entity, and the only
    /// write path that could have filled it - the application form's upload handling - stores
    /// its keys as columns on <c>PersonalDetails</c>, <c>EducationalBackground</c>,
    /// <c>LicensesDetails</c> and <c>ProfessionalExperiences</c> instead. Its navigation
    /// property on <c>EmailInvitationRequest</c> was never populated or read.
    /// </para>
    /// <para>
    /// The dropped columns are the same story at column level: the per-school <c>Address</c> and
    /// <c>Major</c> fields, <c>SchoolSpecificLOAFileKey</c>, <c>CurrentStayFrom</c> and
    /// <c>PermanentTypeOfOwnership</c> are absent from <c>EducationalBackgroundDTO</c> and
    /// <c>AddressDetailsDTO</c>, so no form submission ever set them, and absent from the
    /// report projections and PDF documents, so nothing read them back.
    /// </para>
    /// <para>
    /// <c>PermanentTypeOfOwnership</c> goes while <c>CurrentTypeOfOwnership</c> stays: the form
    /// collects a single <c>TypeOfOwnership</c> and <c>ApplicationFormService</c> maps it onto
    /// the current-address column only, so the permanent one has always been written null.
    /// </para>
    /// <para>
    /// Down re-creates the table and the columns but NOT their contents; the values are gone at
    /// Up. That is acceptable precisely because nothing populated them - the table and columns
    /// are empty or hold only values no shipped code path produced.
    /// </para>
    /// </remarks>
    [DbContext(typeof(ATSDBContext))]
    [Migration("20260922090000_DropUnusedAtsTablesAndColumns")]
    public partial class DropUnusedAtsTablesAndColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentDetails",
                schema: "ats");

            migrationBuilder.DropColumn(
                name: "HighSchoolAddress",
                schema: "ats",
                table: "EducationalBackground");

            migrationBuilder.DropColumn(
                name: "SeniorHighSchoolAddress",
                schema: "ats",
                table: "EducationalBackground");

            migrationBuilder.DropColumn(
                name: "CollegeAddress",
                schema: "ats",
                table: "EducationalBackground");

            migrationBuilder.DropColumn(
                name: "CollegeMajor",
                schema: "ats",
                table: "EducationalBackground");

            migrationBuilder.DropColumn(
                name: "BachelorsAddress",
                schema: "ats",
                table: "EducationalBackground");

            migrationBuilder.DropColumn(
                name: "BachelorsMajor",
                schema: "ats",
                table: "EducationalBackground");

            migrationBuilder.DropColumn(
                name: "MastersAddress",
                schema: "ats",
                table: "EducationalBackground");

            migrationBuilder.DropColumn(
                name: "MastersMajor",
                schema: "ats",
                table: "EducationalBackground");

            migrationBuilder.DropColumn(
                name: "DoctorateAddress",
                schema: "ats",
                table: "EducationalBackground");

            migrationBuilder.DropColumn(
                name: "DoctorateMajor",
                schema: "ats",
                table: "EducationalBackground");

            migrationBuilder.DropColumn(
                name: "SchoolSpecificLOAFileKey",
                schema: "ats",
                table: "EducationalBackground");

            migrationBuilder.DropColumn(
                name: "CurrentStayFrom",
                schema: "ats",
                table: "AddressDetails");

            migrationBuilder.DropColumn(
                name: "PermanentTypeOfOwnership",
                schema: "ats",
                table: "AddressDetails");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PermanentTypeOfOwnership",
                schema: "ats",
                table: "AddressDetails",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentStayFrom",
                schema: "ats",
                table: "AddressDetails",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SchoolSpecificLOAFileKey",
                schema: "ats",
                table: "EducationalBackground",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DoctorateMajor",
                schema: "ats",
                table: "EducationalBackground",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DoctorateAddress",
                schema: "ats",
                table: "EducationalBackground",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MastersMajor",
                schema: "ats",
                table: "EducationalBackground",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MastersAddress",
                schema: "ats",
                table: "EducationalBackground",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BachelorsMajor",
                schema: "ats",
                table: "EducationalBackground",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BachelorsAddress",
                schema: "ats",
                table: "EducationalBackground",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CollegeMajor",
                schema: "ats",
                table: "EducationalBackground",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CollegeAddress",
                schema: "ats",
                table: "EducationalBackground",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SeniorHighSchoolAddress",
                schema: "ats",
                table: "EducationalBackground",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HighSchoolAddress",
                schema: "ats",
                table: "EducationalBackground",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            // Re-created at the width the model had when the table was dropped - 255, set by
            // IncreasedMaxLengthToAllEntityATSMigration - rather than the 100 the initial
            // migration used, so a down-then-up round trip lands where it started.
            migrationBuilder.CreateTable(
                name: "DocumentDetails",
                schema: "ats",
                columns: table => new
                {
                    DocumentDetailsID = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DocumentName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DocumentValue = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    EmailInvitationID = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentDetails", x => x.DocumentDetailsID);
                    table.ForeignKey(
                        name: "FK_DocumentDetails_EmailInvitationRequest_EmailInvitationID",
                        column: x => x.EmailInvitationID,
                        principalSchema: "ats",
                        principalTable: "EmailInvitationRequest",
                        principalColumn: "EmailInvitationID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentDetails_EmailInvitationID",
                schema: "ats",
                table: "DocumentDetails",
                column: "EmailInvitationID");
        }
    }
}
