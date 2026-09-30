using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.ATS
{
    /// <inheritdoc />
    public partial class AddNeedsEmploymentVerificationMarker : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NeedsEmploymentVerification",
                schema: "ats",
                table: "EmailInvitationRequest",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailInvitationRequest_NeedsEmploymentVerification",
                schema: "ats",
                table: "EmailInvitationRequest",
                column: "NeedsEmploymentVerification",
                filter: "\"NeedsEmploymentVerification\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmailInvitationRequest_NeedsEmploymentVerification",
                schema: "ats",
                table: "EmailInvitationRequest");

            migrationBuilder.DropColumn(
                name: "NeedsEmploymentVerification",
                schema: "ats",
                table: "EmailInvitationRequest");
        }
    }
}
