using System;
using System.IO;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.EmploymentVerification
{
    /// <inheritdoc />
    public partial class AddEmploymentVerificationContacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmploymentVerificationContacts",
                schema: "employment_verification",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    EmailAddress = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmploymentVerificationContacts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmploymentVerificationContacts_CompanyName_Id",
                schema: "employment_verification",
                table: "EmploymentVerificationContacts",
                columns: new[] { "CompanyName", "Id" });

            migrationBuilder.CreateIndex(
                name: "UX_EmploymentVerificationContacts_Company_Email",
                schema: "employment_verification",
                table: "EmploymentVerificationContacts",
                columns: new[] { "CompanyName", "EmailAddress" },
                unique: true);

            // The 7,357 seed rows live in a .sql beside the assembly rather than in a C#
            // string literal: a ~950 KB literal is compiled into every build and
            // unreadable in review, while the .sql opens in any SQL tool and diffs
            // cleanly when the source workbook is refreshed. Regenerate it with
            // Tools/generate-ev-contacts-seed.ps1.
            //
            // Seeded in every environment including Testing on purpose. Skipping it
            // there would make the integration suite green against a table shape that
            // production never has.
            migrationBuilder.Sql(ReadSeedScript());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmploymentVerificationContacts",
                schema: "employment_verification");
        }

        private const string SeedScriptFileName = "EmploymentVerificationContactsSeed.sql";

        /// <summary>
        /// Reads the seed script from the output directory, the same way
        /// ATSDatabaseExtensions loads Scripts/quartz_postgres.sql.
        /// </summary>
        private static string ReadSeedScript()
        {
            var scriptPath = Path.Combine(
                AppContext.BaseDirectory,
                "Migrations",
                "EmploymentVerification",
                SeedScriptFileName);

            // Throwing rather than returning empty: a mis-set <Content> item in
            // APIs.csproj would otherwise make this migration a silent no-op and ship an
            // empty directory that looks correctly migrated.
            if (!File.Exists(scriptPath))
            {
                throw new InvalidOperationException(
                    $"Migration seed script '{scriptPath}' was not found. Check the "
                    + "<Content Include=\"Migrations\\**\\*.sql\"> item in APIs.csproj.");
            }

            return File.ReadAllText(scriptPath);
        }
    }
}
