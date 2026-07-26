using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.ATS
{
    /// <inheritdoc />
    public partial class AddedDisputeCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDisputed",
                schema: "ats",
                table: "EmailInvitationRequest");

            migrationBuilder.AddColumn<string>(
                name: "DisputeCategory",
                schema: "ats",
                table: "EmailInvitationRequest",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisputeCategory",
                schema: "ats",
                table: "EmailInvitationRequest");

            migrationBuilder.AddColumn<bool>(
                name: "IsDisputed",
                schema: "ats",
                table: "EmailInvitationRequest",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
