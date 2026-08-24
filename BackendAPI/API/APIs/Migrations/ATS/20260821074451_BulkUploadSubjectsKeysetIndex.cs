using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.ATS
{
    /// <inheritdoc />
    public partial class BulkUploadSubjectsKeysetIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmailInvitationRequest_BulkFileID",
                schema: "ats",
                table: "EmailInvitationRequest");

            migrationBuilder.CreateIndex(
                name: "IX_EmailInvitationRequest_BulkFileID_EmailInvitationID",
                schema: "ats",
                table: "EmailInvitationRequest",
                columns: new[] { "BulkFileID", "EmailInvitationID" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmailInvitationRequest_BulkFileID_EmailInvitationID",
                schema: "ats",
                table: "EmailInvitationRequest");

            migrationBuilder.CreateIndex(
                name: "IX_EmailInvitationRequest_BulkFileID",
                schema: "ats",
                table: "EmailInvitationRequest",
                column: "BulkFileID");
        }
    }
}
