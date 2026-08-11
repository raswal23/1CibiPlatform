using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.ATS
{
    /// <inheritdoc />
    public partial class AddRequestorIdAndClientIdToEmailInvitationRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClientId",
                schema: "ats",
                table: "EmailInvitationRequest",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RequestorId",
                schema: "ats",
                table: "EmailInvitationRequest",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClientId",
                schema: "ats",
                table: "EmailInvitationRequest");

            migrationBuilder.DropColumn(
                name: "RequestorId",
                schema: "ats",
                table: "EmailInvitationRequest");
        }
    }
}
