using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.ATS
{
    /// <inheritdoc />
    public partial class ExpandedPackageDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FollowUpEmail",
                schema: "ats",
                table: "PackageDetails",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PackageDescription",
                schema: "ats",
                table: "PackageDetails",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                schema: "ats",
                table: "PackageDetails",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE ats."PackageDetails"
                SET "UpdatedAt" = "CreatedAt";
                """);

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                schema: "ats",
                table: "PackageDetails",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FollowUpEmail",
                schema: "ats",
                table: "PackageDetails");

            migrationBuilder.DropColumn(
                name: "PackageDescription",
                schema: "ats",
                table: "PackageDetails");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                schema: "ats",
                table: "PackageDetails");
        }
    }
}
