using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.PhilSys
{
    /// <inheritdoc />
    public partial class ImageBase64ToImageByte : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ImageinBase64",
                schema: "philsys",
                table: "PhilSysTransaction",
                newName: "ImageinByte");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ImageinByte",
                schema: "philsys",
                table: "PhilSysTransaction",
                newName: "ImageinBase64");
        }
    }
}
