using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.ATS
{
    /// <inheritdoc />
    public partial class AddBulkFileClaimATSMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ClaimedAt",
                schema: "ats",
                table: "BulkUploadFileDetails",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BulkUploadFileDetails_Status",
                schema: "ats",
                table: "BulkUploadFileDetails",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BulkUploadFileDetails_Status",
                schema: "ats",
                table: "BulkUploadFileDetails");

            migrationBuilder.DropColumn(
                name: "ClaimedAt",
                schema: "ats",
                table: "BulkUploadFileDetails");
        }
    }
}
