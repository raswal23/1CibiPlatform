using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.ATS
{
    /// <inheritdoc />
    public partial class AddedReportDetailsAndArchiveReportDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArchiveReport",
                schema: "ats",
                columns: table => new
                {
                    ArchiveReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmailInvitationRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportStatus = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ReportFileName = table.Column<string>(type: "character varying(525)", maxLength: 525, nullable: false),
                    ReportFileKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ReportUploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchiveReport", x => x.ArchiveReportId);
                    table.ForeignKey(
                        name: "FK_ArchiveReport_EmailInvitationRequest_EmailInvitationRequest~",
                        column: x => x.EmailInvitationRequestId,
                        principalSchema: "ats",
                        principalTable: "EmailInvitationRequest",
                        principalColumn: "EmailInvitationID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReportDetails",
                schema: "ats",
                columns: table => new
                {
                    ReportFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmailInvitationRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    HitStatus = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ReportStatus = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ReportFileName = table.Column<string>(type: "character varying(525)", maxLength: 525, nullable: false),
                    ReportFileKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ReportUploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportDetails", x => x.ReportFileId);
                    table.ForeignKey(
                        name: "FK_ReportDetails_EmailInvitationRequest_EmailInvitationRequest~",
                        column: x => x.EmailInvitationRequestId,
                        principalSchema: "ats",
                        principalTable: "EmailInvitationRequest",
                        principalColumn: "EmailInvitationID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveReport_EmailInvitationRequestId",
                schema: "ats",
                table: "ArchiveReport",
                column: "EmailInvitationRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportDetails_EmailInvitationRequestId_ReportStatus",
                schema: "ats",
                table: "ReportDetails",
                columns: new[] { "EmailInvitationRequestId", "ReportStatus" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArchiveReport",
                schema: "ats");

            migrationBuilder.DropTable(
                name: "ReportDetails",
                schema: "ats");
        }
    }
}
