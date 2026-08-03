using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.ATS
{
    /// <inheritdoc />
    public partial class AddedTicketStatusForEmailInvitationRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsFormCompleted",
                schema: "ats",
                table: "EmailInvitationRequest",
                newName: "ApplicationFormStatus");
			
			migrationBuilder.AddColumn<string>(
                name: "TicketStatus",
                schema: "ats",
                table: "EmailInvitationRequest",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TicketStatus",
                schema: "ats",
                table: "EmailInvitationRequest");

            migrationBuilder.RenameColumn(
                name: "ApplicationFormStatus",
                schema: "ats",
                table: "EmailInvitationRequest",
                newName: "IsFormCompleted");
        }
    }
}
