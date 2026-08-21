using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.ATS
{
    /// <inheritdoc />
    public partial class BulkUploadDashboardKeysetIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_BulkUploadFileDetails_DateCreated_FileID",
                schema: "ats",
                table: "BulkUploadFileDetails",
                columns: new[] { "DateCreated", "FileID" },
                descending: new[] { true, false });

            migrationBuilder.CreateIndex(
                name: "IX_BulkUploadFileDetails_Status_DateCreated_FileID",
                schema: "ats",
                table: "BulkUploadFileDetails",
                columns: new[] { "Status", "DateCreated", "FileID" },
                descending: new[] { false, true, false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BulkUploadFileDetails_DateCreated_FileID",
                schema: "ats",
                table: "BulkUploadFileDetails");

            migrationBuilder.DropIndex(
                name: "IX_BulkUploadFileDetails_Status_DateCreated_FileID",
                schema: "ats",
                table: "BulkUploadFileDetails");
        }
    }
}
