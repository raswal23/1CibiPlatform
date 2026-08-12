using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace APIs.Migrations.PlatformLogging
{
    /// <inheritdoc />
    public partial class InitialPlatformLogging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "logging");

            migrationBuilder.CreateTable(
                name: "log_events",
                schema: "logging",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    level = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    message_template = table.Column<string>(type: "text", nullable: true),
                    rendered_message = table.Column<string>(type: "text", nullable: false),
                    exception = table.Column<string>(type: "text", nullable: true),
                    properties = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "jsonb_build_object()"),
                    platform = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    application = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    environment = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    source_context = table.Column<string>(type: "text", nullable: true),
                    trace_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    request_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_log_events", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_log_events_application_occurred_at",
                schema: "logging",
                table: "log_events",
                columns: new[] { "application", "occurred_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_log_events_level_occurred_at",
                schema: "logging",
                table: "log_events",
                columns: new[] { "level", "occurred_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_log_events_occurred_at_id",
                schema: "logging",
                table: "log_events",
                columns: new[] { "occurred_at", "id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_log_events_trace_id_occurred_at",
                schema: "logging",
                table: "log_events",
                columns: new[] { "trace_id", "occurred_at" },
                descending: new[] { false, true },
                filter: "trace_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "log_events",
                schema: "logging");
        }
    }
}
