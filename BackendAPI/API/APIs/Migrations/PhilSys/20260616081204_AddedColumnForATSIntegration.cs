using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.PhilSys
{
    /// <inheritdoc />
    public partial class AddedColumnForATSIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ATSSession",
                schema: "philsys",
                table: "PhilSysTransaction",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ATSSession",
                schema: "philsys",
                table: "PhilSysTransaction");
        }
    }
}
