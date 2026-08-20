using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.ATS
{
    /// <inheritdoc />
    public partial class AddEmailRetryAndBulkFileLinkATSMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BulkFileID",
                schema: "ats",
                table: "EmailInvitationRequest",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmailSendAttempts",
                schema: "ats",
                table: "EmailInvitationRequest",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_EmailInvitationRequest_BulkFileID",
                schema: "ats",
                table: "EmailInvitationRequest",
                column: "BulkFileID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmailInvitationRequest_BulkFileID",
                schema: "ats",
                table: "EmailInvitationRequest");

            migrationBuilder.DropColumn(
                name: "BulkFileID",
                schema: "ats",
                table: "EmailInvitationRequest");

            migrationBuilder.DropColumn(
                name: "EmailSendAttempts",
                schema: "ats",
                table: "EmailInvitationRequest");
        }
    }
}
