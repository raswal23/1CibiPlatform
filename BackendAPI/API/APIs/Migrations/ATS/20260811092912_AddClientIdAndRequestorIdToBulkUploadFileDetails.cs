using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.ATS
{
    /// <inheritdoc />
    public partial class AddClientIdAndRequestorIdToBulkUploadFileDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClientId",
                schema: "ats",
                table: "BulkUploadFileDetails",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RequestorId",
                schema: "ats",
                table: "BulkUploadFileDetails",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClientId",
                schema: "ats",
                table: "BulkUploadFileDetails");

            migrationBuilder.DropColumn(
                name: "RequestorId",
                schema: "ats",
                table: "BulkUploadFileDetails");
        }
    }
}
