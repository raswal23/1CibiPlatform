using ATS.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.ATS
{
    /// <summary>
    /// Classifies every pre-existing order and bulk file as manual screening, and
    /// repairs databases that recorded 20260914150000 before it carried the bulk-file
    /// column.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hand-written (the EF CLI could not run in this environment); the attributes
    /// below carry what the Designer file would normally declare. No model change, so
    /// the snapshot is untouched.
    /// </para>
    /// <para>
    /// Every statement is idempotent on purpose. 20260914150000 grew the
    /// BulkUploadFileDetails column after some databases had already applied it, and a
    /// recorded migration is never re-run - so those databases are missing a column
    /// that EF believes exists, and the bulk claim query fails with "The required
    /// column 'AutoChasing' was not present in the results of a 'FromSql' operation."
    /// A database that applied the complete 20260914150000 already has the column and
    /// simply skips the ALTER.
    /// </para>
    /// </remarks>
    [DbContext(typeof(ATSDBContext))]
    [Migration("20260914160000_BackfillLegacyAutoChasing")]
    public partial class BackfillLegacyAutoChasing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE ats."BulkUploadFileDetails"
                    ADD COLUMN IF NOT EXISTS "AutoChasing" boolean;
                """);

            // Data screening did not exist when these rows were created, so every one of
            // them is a manual order. Leaving them NULL is not neutral: the invitation
            // worker claims "AutoChasing" IS TRUE, so any legacy order still waiting to be
            // emailed would sit in the queue forever.
            migrationBuilder.Sql(
                """
                UPDATE ats."EmailInvitationRequest"
                SET "AutoChasing" = TRUE
                WHERE "AutoChasing" IS NULL;
                """);

            // The same reasoning one level up: a bulk file stamps its own screening type
            // onto every order it creates, so a NULL legacy file would produce a fresh
            // batch of orders that the worker also refuses to email.
            migrationBuilder.Sql(
                """
                UPDATE ats."BulkUploadFileDetails"
                SET "AutoChasing" = TRUE
                WHERE "AutoChasing" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Deliberately empty. A backfilled TRUE is indistinguishable from one a user
            // chose, so reverting would also unclassify real manual orders. The column
            // itself is dropped by 20260914150000's Down.
        }
    }
}
