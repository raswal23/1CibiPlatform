using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.ATS
{
    /// <inheritdoc />
    public partial class AddedStatusColumnForEmailEntityATSMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "ats",
                table: "EmailInvitationRequest",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                schema: "ats",
                table: "EmailInvitationRequest");
        }
    }
}
