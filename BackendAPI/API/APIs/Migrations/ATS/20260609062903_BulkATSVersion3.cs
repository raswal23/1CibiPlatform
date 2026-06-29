using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.ATS
{
    /// <inheritdoc />
    public partial class BulkATSVersion3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_FileDetails",
                schema: "ats",
                table: "FileDetails");

            migrationBuilder.RenameTable(
                name: "FileDetails",
                schema: "ats",
                newName: "BulkUploadFileDetails",
                newSchema: "ats");

            migrationBuilder.AddPrimaryKey(
                name: "PK_BulkUploadFileDetails",
                schema: "ats",
                table: "BulkUploadFileDetails",
                column: "FileID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_BulkUploadFileDetails",
                schema: "ats",
                table: "BulkUploadFileDetails");

            migrationBuilder.RenameTable(
                name: "BulkUploadFileDetails",
                schema: "ats",
                newName: "FileDetails",
                newSchema: "ats");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FileDetails",
                schema: "ats",
                table: "FileDetails",
                column: "FileID");
        }
    }
}
