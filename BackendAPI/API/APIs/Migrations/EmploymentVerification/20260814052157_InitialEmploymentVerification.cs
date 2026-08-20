using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmploymentVerification.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialEmploymentVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "employment_verification");

            migrationBuilder.CreateTable(
                name: "EmploymentVerificationRequests",
                schema: "employment_verification",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AtsSubjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    CandidateName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PreviousEmployer = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Position = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EmploymentStartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EmploymentEndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    HrName = table.Column<string>(type: "text", nullable: true),
                    HrEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    VerificationTokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TokenExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResponseNotes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmploymentVerificationRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmploymentVerificationRequests_Status_RequestedAt",
                schema: "employment_verification",
                table: "EmploymentVerificationRequests",
                columns: new[] { "Status", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EmploymentVerificationRequests_VerificationTokenHash",
                schema: "employment_verification",
                table: "EmploymentVerificationRequests",
                column: "VerificationTokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmploymentVerificationRequests",
                schema: "employment_verification");
        }
    }
}
