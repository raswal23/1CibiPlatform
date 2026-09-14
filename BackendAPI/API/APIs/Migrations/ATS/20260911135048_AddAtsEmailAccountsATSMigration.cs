using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace APIs.Migrations.ATS
{
    /// <inheritdoc />
    public partial class AddAtsEmailAccountsATSMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmailAccounts",
                schema: "ats",
                columns: table => new
                {
                    AtsEmailAccountId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DisplayName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    EmailAddress = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    SmtpHost = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    SmtpPort = table.Column<int>(type: "integer", nullable: false),
                    EncryptedPassword = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DailySendLimit = table.Column<int>(type: "integer", nullable: false),
                    VerificationStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConsecutiveFailureCount = table.Column<int>(type: "integer", nullable: false),
                    CoolingDownUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastFailureReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LastSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailAccounts", x => x.AtsEmailAccountId);
                });

            migrationBuilder.CreateTable(
                name: "EmailAccountOtp",
                schema: "ats",
                columns: table => new
                {
                    AtsEmailAccountOtpId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AtsEmailAccountId = table.Column<int>(type: "integer", nullable: false),
                    Purpose = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OtpCodeHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    IsUsed = table.Column<bool>(type: "boolean", nullable: false),
                    PendingChangesJson = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailAccountOtp", x => x.AtsEmailAccountOtpId);
                    table.ForeignKey(
                        name: "FK_EmailAccountOtp_EmailAccounts_AtsEmailAccountId",
                        column: x => x.AtsEmailAccountId,
                        principalSchema: "ats",
                        principalTable: "EmailAccounts",
                        principalColumn: "AtsEmailAccountId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmailSendLog",
                schema: "ats",
                columns: table => new
                {
                    AtsEmailSendLogId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AtsEmailAccountId = table.Column<int>(type: "integer", nullable: false),
                    RecipientCount = table.Column<int>(type: "integer", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailSendLog", x => x.AtsEmailSendLogId);
                    table.ForeignKey(
                        name: "FK_EmailSendLog_EmailAccounts_AtsEmailAccountId",
                        column: x => x.AtsEmailAccountId,
                        principalSchema: "ats",
                        principalTable: "EmailAccounts",
                        principalColumn: "AtsEmailAccountId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailAccountOtp_AtsEmailAccountId_Purpose_IsUsed_CreatedAt",
                schema: "ats",
                table: "EmailAccountOtp",
                columns: new[] { "AtsEmailAccountId", "Purpose", "IsUsed", "CreatedAt" },
                descending: new[] { false, false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_EmailAccounts_EmailAddress",
                schema: "ats",
                table: "EmailAccounts",
                column: "EmailAddress",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailAccounts_IsActive_VerificationStatus_Priority",
                schema: "ats",
                table: "EmailAccounts",
                columns: new[] { "IsActive", "VerificationStatus", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailAccounts_Priority",
                schema: "ats",
                table: "EmailAccounts",
                column: "Priority",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailSendLog_AtsEmailAccountId_SentAt",
                schema: "ats",
                table: "EmailSendLog",
                columns: new[] { "AtsEmailAccountId", "SentAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailAccountOtp",
                schema: "ats");

            migrationBuilder.DropTable(
                name: "EmailSendLog",
                schema: "ats");

            migrationBuilder.DropTable(
                name: "EmailAccounts",
                schema: "ats");
        }
    }
}
