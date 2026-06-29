using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.ATS
{
    /// <inheritdoc />
    public partial class BulkATSVersion5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EmailSentAt",
                schema: "ats",
                table: "EmailInvitationRequest",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UploadedByUserId",
                schema: "ats",
                table: "BulkUploadFileDetails",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailSentAt",
                schema: "ats",
                table: "EmailInvitationRequest");

            migrationBuilder.DropColumn(
                name: "UploadedByUserId",
                schema: "ats",
                table: "BulkUploadFileDetails");
        }
    }
}
