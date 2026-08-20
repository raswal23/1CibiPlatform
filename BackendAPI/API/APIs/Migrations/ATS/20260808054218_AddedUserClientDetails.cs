using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.ATS
{
    /// <inheritdoc />
    public partial class AddedUserClientDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserClientDetails",
                schema: "ats",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserClientDetails", x => x.UserId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserClientDetails_ClientId",
                schema: "ats",
                table: "UserClientDetails",
                column: "ClientId");

            migrationBuilder.Sql(
                """
                INSERT INTO ats."UserClientDetails" ("UserId", "ClientId", "CreatedAt", "UpdatedAt")
                SELECT latest."UserId", latest."ClientId", timestamps."CreatedAt", timestamps."UpdatedAt"
                FROM (
                    SELECT DISTINCT ON ("UserId") "UserId", "ClientId"
                    FROM ats."UserDetails"
                    ORDER BY "UserId", "UpdatedAt" DESC
                ) AS latest
                INNER JOIN (
                    SELECT "UserId", MIN("CreatedAt") AS "CreatedAt", MAX("UpdatedAt") AS "UpdatedAt"
                    FROM ats."UserDetails"
                    GROUP BY "UserId"
                ) AS timestamps ON timestamps."UserId" = latest."UserId"
                ON CONFLICT ("UserId") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserClientDetails",
                schema: "ats");
        }
    }
}
