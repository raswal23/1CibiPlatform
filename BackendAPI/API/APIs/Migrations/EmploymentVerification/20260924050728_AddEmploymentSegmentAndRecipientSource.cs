using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.EmploymentVerification
{
    /// <inheritdoc />
    public partial class AddEmploymentSegmentAndRecipientSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<short>(
                name: "EmploymentSegment",
                schema: "employment_verification",
                table: "EmploymentVerificationRequests",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecipientSource",
                schema: "employment_verification",
                table: "EmploymentVerificationRequests",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmploymentVerificationRequests_AtsSubjectId_EmploymentSegme~",
                schema: "employment_verification",
                table: "EmploymentVerificationRequests",
                columns: new[] { "AtsSubjectId", "EmploymentSegment" });

            // Backfill. Every request raised before this migration came from the ATS
            // provider, which only ever read the Emp1* columns - so they all cover the
            // first employment slot, and all went to an address the candidate supplied.
            //
            // Not cosmetic: the availability check now ignores rows with no segment, so
            // leaving these NULL would stop live requests from blocking their segment
            // and the job would email those employers a second time.
            migrationBuilder.Sql(
                """
                UPDATE employment_verification."EmploymentVerificationRequests"
                SET "EmploymentSegment" = 1,
                    "RecipientSource" = 'CandidateSupplied'
                WHERE "AtsSubjectId" IS NOT NULL
                  AND "EmploymentSegment" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmploymentVerificationRequests_AtsSubjectId_EmploymentSegme~",
                schema: "employment_verification",
                table: "EmploymentVerificationRequests");

            migrationBuilder.DropColumn(
                name: "EmploymentSegment",
                schema: "employment_verification",
                table: "EmploymentVerificationRequests");

            migrationBuilder.DropColumn(
                name: "RecipientSource",
                schema: "employment_verification",
                table: "EmploymentVerificationRequests");
        }
    }
}
