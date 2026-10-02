using ATS.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.ATS
{
    /// <summary>
    /// Screening type per package: true = manual screening, false = data screening,
    /// null = not set. Existing packages stay null so the UI shows "Not set" instead
    /// of silently classifying them.
    /// </summary>
    /// <remarks>
    /// Hand-written (the EF CLI could not run in this environment); the attributes
    /// below carry what the Designer file would normally declare, and the model
    /// snapshot was updated in the same commit.
    /// </remarks>
    [DbContext(typeof(ATSDBContext))]
    [Migration("20260914120000_AddAutoChasingToPackageDetails")]
    public partial class AddAutoChasingToPackageDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoChasing",
                schema: "ats",
                table: "PackageDetails",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoChasing",
                schema: "ats",
                table: "PackageDetails");
        }
    }
}
