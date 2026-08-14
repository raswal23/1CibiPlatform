using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace APIs.Migrations.ATS
{
    /// <inheritdoc />
    public partial class UpdatedClientPackageRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE ats."PackageDetails" RENAME TO "PackageDetails_Legacy";
                ALTER TABLE ats."PackageDetails_Legacy" RENAME CONSTRAINT "PK_PackageDetails" TO "PK_PackageDetails_Legacy";
                ALTER INDEX ats."IX_PackageDetails_PackageName" RENAME TO "IX_PackageDetails_Legacy_PackageName";

                ALTER TABLE ats."ClientDetails" RENAME TO "ClientDetails_Legacy";
                ALTER TABLE ats."ClientDetails_Legacy" RENAME CONSTRAINT "PK_ClientDetails" TO "PK_ClientDetails_Legacy";
                ALTER INDEX ats."IX_ClientDetails_ClientName" RENAME TO "IX_ClientDetails_Legacy_ClientName";
                """);

            migrationBuilder.CreateTable(
                name: "PackageDetails",
                schema: "ats",
                columns: table => new
                {
                    PackageId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PackageName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageDetails", x => x.PackageId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PackageDetails_PackageName",
                schema: "ats",
                table: "PackageDetails",
                column: "PackageName",
                unique: true);

            migrationBuilder.Sql(
                """
                INSERT INTO ats."PackageDetails" ("PackageName", "IsActive", "CreatedAt")
                SELECT "PackageName", "IsActive", "CreatedAt"
                FROM ats."PackageDetails_Legacy"
                ORDER BY "CreatedAt", "PackageId";

                INSERT INTO ats."PackageDetails" ("PackageName", "IsActive", "CreatedAt")
                SELECT 'Legacy Unassigned', FALSE, COALESCE(MIN("CreatedAt"), NOW())
                FROM ats."ClientDetails_Legacy"
                HAVING COUNT(*) > 0
                ON CONFLICT ("PackageName") DO NOTHING;
                """);

            migrationBuilder.CreateTable(
                name: "ClientDetails",
                schema: "ats",
                columns: table => new
                {
                    ClientId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClientName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ClientDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    PackageId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientDetails", x => new { x.ClientId, x.PackageId });
                    table.ForeignKey(
                        name: "FK_ClientDetails_PackageDetails_PackageId",
                        column: x => x.PackageId,
                        principalSchema: "ats",
                        principalTable: "PackageDetails",
                        principalColumn: "PackageId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientDetails_ClientName",
                schema: "ats",
                table: "ClientDetails",
                column: "ClientName");

            migrationBuilder.CreateIndex(
                name: "IX_ClientDetails_PackageId",
                schema: "ats",
                table: "ClientDetails",
                column: "PackageId");

            migrationBuilder.Sql(
                """
                INSERT INTO ats."ClientDetails"
                    ("ClientId", "ClientName", "ClientDescription", "IsActive", "PackageId", "CreatedAt", "UpdatedAt")
                SELECT
                    ROW_NUMBER() OVER (ORDER BY legacy."CreatedAt", legacy."ClientId")::integer,
                    LEFT(legacy."ClientName", 100),
                    '',
                    legacy."IsActive",
                    package."PackageId",
                    legacy."CreatedAt",
                    legacy."CreatedAt"
                FROM ats."ClientDetails_Legacy" AS legacy
                CROSS JOIN LATERAL (
                    SELECT "PackageId"
                    FROM ats."PackageDetails"
                    WHERE "PackageName" = 'Legacy Unassigned'
                    LIMIT 1
                ) AS package;

                SELECT setval(
                    pg_get_serial_sequence('ats."ClientDetails"', 'ClientId'),
                    COALESCE((SELECT MAX("ClientId") FROM ats."ClientDetails"), 0) + 1,
                    false);

                DROP TABLE ats."ClientDetails_Legacy";
                DROP TABLE ats."PackageDetails_Legacy";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE ats."ClientDetails" RENAME TO "ClientDetails_Modern";
                ALTER TABLE ats."ClientDetails_Modern" RENAME CONSTRAINT "PK_ClientDetails" TO "PK_ClientDetails_Modern";
                ALTER TABLE ats."ClientDetails_Modern" RENAME CONSTRAINT "FK_ClientDetails_PackageDetails_PackageId" TO "FK_ClientDetails_Modern_PackageDetails_PackageId";
                ALTER INDEX ats."IX_ClientDetails_ClientName" RENAME TO "IX_ClientDetails_Modern_ClientName";
                ALTER INDEX ats."IX_ClientDetails_PackageId" RENAME TO "IX_ClientDetails_Modern_PackageId";

                ALTER TABLE ats."PackageDetails" RENAME TO "PackageDetails_Modern";
                ALTER TABLE ats."PackageDetails_Modern" RENAME CONSTRAINT "PK_PackageDetails" TO "PK_PackageDetails_Modern";
                ALTER INDEX ats."IX_PackageDetails_PackageName" RENAME TO "IX_PackageDetails_Modern_PackageName";
                """);

            migrationBuilder.CreateTable(
                name: "PackageDetails",
                schema: "ats",
                columns: table => new
                {
                    PackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    PackageName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageDetails", x => x.PackageId);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO ats."PackageDetails" ("PackageId", "PackageName", "IsActive", "CreatedAt")
                SELECT md5("PackageId"::text || ':' || "PackageName")::uuid, "PackageName", "IsActive", "CreatedAt"
                FROM ats."PackageDetails_Modern"
                WHERE "PackageName" <> 'Legacy Unassigned';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_PackageDetails_PackageName",
                schema: "ats",
                table: "PackageDetails",
                column: "PackageName",
                unique: true);

            migrationBuilder.CreateTable(
                name: "ClientDetails",
                schema: "ats",
                columns: table => new
                {
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientDetails", x => x.ClientId);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO ats."ClientDetails" ("ClientId", "ClientName", "IsActive", "CreatedAt")
                SELECT
                    md5("ClientId"::text || ':' || MIN("ClientName"))::uuid,
                    MIN("ClientName"),
                    BOOL_AND("IsActive"),
                    MIN("CreatedAt")
                FROM ats."ClientDetails_Modern"
                GROUP BY "ClientId";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ClientDetails_ClientName",
                schema: "ats",
                table: "ClientDetails",
                column: "ClientName",
                unique: true);

            migrationBuilder.Sql(
                """
                DROP TABLE ats."ClientDetails_Modern";
                DROP TABLE ats."PackageDetails_Modern";
                """);
        }
    }
}
