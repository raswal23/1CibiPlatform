using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.ATS
{
    /// <inheritdoc />
    public partial class KeysetPaginationIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ClientDetails_ClientName",
                schema: "ats",
                table: "ClientDetails");

            migrationBuilder.CreateIndex(
                name: "IX_UserDetails_UserName_UserEmail_UserId",
                schema: "ats",
                table: "UserDetails",
                columns: new[] { "UserName", "UserEmail", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailInvitationRequest_FirstName_LastName_EmailInvitationID",
                schema: "ats",
                table: "EmailInvitationRequest",
                columns: new[] { "FirstName", "LastName", "EmailInvitationID" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailInvitationRequest_OrderCompletedAt_EmailInvitationID",
                schema: "ats",
                table: "EmailInvitationRequest",
                columns: new[] { "OrderCompletedAt", "EmailInvitationID" },
                descending: new[] { true, false });

            migrationBuilder.CreateIndex(
                name: "IX_EmailInvitationRequest_OrderCreatedAt_EmailInvitationID",
                schema: "ats",
                table: "EmailInvitationRequest",
                columns: new[] { "OrderCreatedAt", "EmailInvitationID" },
                descending: new[] { true, false });

            migrationBuilder.CreateIndex(
                name: "IX_EmailInvitationRequest_OrderStatus_EmailInvitationID",
                schema: "ats",
                table: "EmailInvitationRequest",
                columns: new[] { "OrderStatus", "EmailInvitationID" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientDetails_ClientName_ClientId",
                schema: "ats",
                table: "ClientDetails",
                columns: new[] { "ClientName", "ClientId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserDetails_UserName_UserEmail_UserId",
                schema: "ats",
                table: "UserDetails");

            migrationBuilder.DropIndex(
                name: "IX_EmailInvitationRequest_FirstName_LastName_EmailInvitationID",
                schema: "ats",
                table: "EmailInvitationRequest");

            migrationBuilder.DropIndex(
                name: "IX_EmailInvitationRequest_OrderCompletedAt_EmailInvitationID",
                schema: "ats",
                table: "EmailInvitationRequest");

            migrationBuilder.DropIndex(
                name: "IX_EmailInvitationRequest_OrderCreatedAt_EmailInvitationID",
                schema: "ats",
                table: "EmailInvitationRequest");

            migrationBuilder.DropIndex(
                name: "IX_EmailInvitationRequest_OrderStatus_EmailInvitationID",
                schema: "ats",
                table: "EmailInvitationRequest");

            migrationBuilder.DropIndex(
                name: "IX_ClientDetails_ClientName_ClientId",
                schema: "ats",
                table: "ClientDetails");

            migrationBuilder.CreateIndex(
                name: "IX_ClientDetails_ClientName",
                schema: "ats",
                table: "ClientDetails",
                column: "ClientName");
        }
    }
}
