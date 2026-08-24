using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.Auth
{
    /// <inheritdoc />
    public partial class KeysetPaginationIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AuthUsers_LastName_FirstName_Id",
                table: "AuthUsers",
                columns: new[] { "LastName", "FirstName", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuthUsers_LastName_FirstName_Id",
                table: "AuthUsers");
        }
    }
}
