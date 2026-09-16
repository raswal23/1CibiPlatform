using ATS.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.ATS
{
    /// <summary>
    /// Candidate identity captured at order entry (date of birth, SSS number, TIN
    /// number) plus the screening-type snapshot on orders and bulk files. The
    /// identity fields are required (by the web validator) only for data-screening
    /// orders, where no application form is sent; everything is nullable so manual,
    /// bulk, public API and legacy rows are unaffected.
    /// </summary>
    /// <remarks>
    /// Hand-written (the EF CLI could not run in this environment); the attributes
    /// below carry what the Designer file would normally declare, and the model
    /// snapshot was updated in the same commit.
    /// </remarks>
    [DbContext(typeof(ATSDBContext))]
    [Migration("20260914150000_AddCandidateIdentityToEmailInvitationRequest")]
    public partial class AddCandidateIdentityToEmailInvitationRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoChasing",
                schema: "ats",
                table: "EmailInvitationRequest",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DateOfBirth",
                schema: "ats",
                table: "EmailInvitationRequest",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SSSNumber",
                schema: "ats",
                table: "EmailInvitationRequest",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TINNumber",
                schema: "ats",
                table: "EmailInvitationRequest",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AutoChasing",
                schema: "ats",
                table: "BulkUploadFileDetails",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoChasing",
                schema: "ats",
                table: "EmailInvitationRequest");

            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                schema: "ats",
                table: "EmailInvitationRequest");

            migrationBuilder.DropColumn(
                name: "SSSNumber",
                schema: "ats",
                table: "EmailInvitationRequest");

            migrationBuilder.DropColumn(
                name: "TINNumber",
                schema: "ats",
                table: "EmailInvitationRequest");

            migrationBuilder.DropColumn(
                name: "AutoChasing",
                schema: "ats",
                table: "BulkUploadFileDetails");
        }
    }
}
