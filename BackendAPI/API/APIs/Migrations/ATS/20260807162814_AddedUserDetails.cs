using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.ATS
{
    /// <inheritdoc />
    public partial class AddedUserDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserDetails",
                schema: "ats",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModuleId = table.Column<int>(type: "integer", nullable: false),
                    UserName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UserEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ClientId = table.Column<int>(type: "integer", nullable: false),
                    Site = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RoleId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDetails", x => new { x.UserId, x.ModuleId });
                    table.ForeignKey(
                        name: "FK_UserDetails_ModuleDetails_ModuleId",
                        column: x => x.ModuleId,
                        principalSchema: "ats",
                        principalTable: "ModuleDetails",
                        principalColumn: "ModuleId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserDetails_RoleDetails_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "ats",
                        principalTable: "RoleDetails",
                        principalColumn: "RoleId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserDetails_ClientId",
                schema: "ats",
                table: "UserDetails",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_UserDetails_ModuleId",
                schema: "ats",
                table: "UserDetails",
                column: "ModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserDetails_RoleId",
                schema: "ats",
                table: "UserDetails",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserDetails_UserEmail",
                schema: "ats",
                table: "UserDetails",
                column: "UserEmail");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserDetails",
                schema: "ats");
        }
    }
}
