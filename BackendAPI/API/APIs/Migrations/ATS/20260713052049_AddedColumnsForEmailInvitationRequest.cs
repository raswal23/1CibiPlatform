using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.ATS
{
    /// <inheritdoc />
    public partial class AddedColumnsForEmailInvitationRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TicketStatus",
                schema: "ats",
                table: "EmailInvitationRequest",
                newName: "OrderStatus");

            migrationBuilder.AddColumn<DateTime>(
                name: "DisputedAt",
                schema: "ats",
                table: "EmailInvitationRequest",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDisputed",
                schema: "ats",
                table: "EmailInvitationRequest",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "OrderCompletedAt",
                schema: "ats",
                table: "EmailInvitationRequest",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OrderCreatedAt",
                schema: "ats",
                table: "EmailInvitationRequest",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisputedAt",
                schema: "ats",
                table: "EmailInvitationRequest");

            migrationBuilder.DropColumn(
                name: "IsDisputed",
                schema: "ats",
                table: "EmailInvitationRequest");

            migrationBuilder.DropColumn(
                name: "OrderCompletedAt",
                schema: "ats",
                table: "EmailInvitationRequest");

            migrationBuilder.DropColumn(
                name: "OrderCreatedAt",
                schema: "ats",
                table: "EmailInvitationRequest");

            migrationBuilder.RenameColumn(
                name: "OrderStatus",
                schema: "ats",
                table: "EmailInvitationRequest",
                newName: "TicketStatus");
        }
    }
}
