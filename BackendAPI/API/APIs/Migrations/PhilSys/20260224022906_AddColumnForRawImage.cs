using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.PhilSys
{
    /// <inheritdoc />
    public partial class AddColumnForRawImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "ImageinBase64",
                schema: "philsys",
                table: "PhilSysTransaction",
                type: "bytea",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageinBase64",
                schema: "philsys",
                table: "PhilSysTransaction");
        }
    }
}
