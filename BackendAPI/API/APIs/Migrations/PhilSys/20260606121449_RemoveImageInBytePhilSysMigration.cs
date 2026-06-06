using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.PhilSys
{
    /// <inheritdoc />
    public partial class RemoveImageInBytePhilSysMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageinByte",
                schema: "philsys",
                table: "PhilSysTransaction");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "ImageinByte",
                schema: "philsys",
                table: "PhilSysTransaction",
                type: "bytea",
                nullable: true);
        }
    }
}
