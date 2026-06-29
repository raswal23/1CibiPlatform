using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.ATS
{
    /// <inheritdoc />
    public partial class BulkATSVersion4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OrderType",
                schema: "ats",
                table: "BulkUploadFileDetails",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PackageType",
                schema: "ats",
                table: "BulkUploadFileDetails",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OrderType",
                schema: "ats",
                table: "BulkUploadFileDetails");

            migrationBuilder.DropColumn(
                name: "PackageType",
                schema: "ats",
                table: "BulkUploadFileDetails");
        }
    }
}
