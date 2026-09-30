using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace APIs.Migrations.ATS
{
    /// <inheritdoc />
    public partial class AddEmailProcessDetailsATSMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmailProcessDetails",
                schema: "ats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmailProcess = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CCEmail = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailProcessDetails", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailProcessDetails_EmailProcess",
                schema: "ats",
                table: "EmailProcessDetails",
                column: "EmailProcess",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailProcessDetails",
                schema: "ats");
        }
    }
}
