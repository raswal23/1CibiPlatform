using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.ATS
{
    /// <inheritdoc />
    public partial class AddConsentFormColumnsToSignatureDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SignatureFileName",
                schema: "ats",
                table: "SignatureDetails");

            migrationBuilder.RenameColumn(
                name: "SignatureFileKey",
                schema: "ats",
                table: "SignatureDetails",
                newName: "ConsentFormFileName");

            migrationBuilder.AddColumn<string>(
                name: "ConsentFormFileKey",
                schema: "ats",
                table: "SignatureDetails",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConsentGeneratedAt",
                schema: "ats",
                table: "SignatureDetails",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConsentFormFileKey",
                schema: "ats",
                table: "SignatureDetails");

            migrationBuilder.DropColumn(
                name: "ConsentGeneratedAt",
                schema: "ats",
                table: "SignatureDetails");

            migrationBuilder.RenameColumn(
                name: "ConsentFormFileName",
                schema: "ats",
                table: "SignatureDetails",
                newName: "SignatureFileKey");

            migrationBuilder.AddColumn<string>(
                name: "SignatureFileName",
                schema: "ats",
                table: "SignatureDetails",
                type: "text",
                nullable: true);
        }
    }
}
