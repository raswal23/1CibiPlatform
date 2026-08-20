using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.ATS
{
    /// <inheritdoc />
    public partial class AddEmailSentStatusIndexATSMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_EmailInvitationRequest_EmailSentStatus",
                schema: "ats",
                table: "EmailInvitationRequest",
                column: "EmailSentStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmailInvitationRequest_EmailSentStatus",
                schema: "ats",
                table: "EmailInvitationRequest");
        }
    }
}
