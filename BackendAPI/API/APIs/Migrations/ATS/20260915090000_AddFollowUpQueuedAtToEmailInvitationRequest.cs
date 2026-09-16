using ATS.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.ATS
{
    /// <summary>
    /// Adds the fire-once stamp for the package follow-up reminder, and marks every
    /// pre-existing order as already chased.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hand-written (the EF CLI could not run in this environment); the attributes below
    /// carry what the Designer file would normally declare. The model snapshot is updated
    /// in the same commit.
    /// </para>
    /// <para>
    /// The backfill is the load-bearing half. PackageDetails.FollowUpEmail has been
    /// collected by the package form for a long time without any consumer, so the first
    /// pass of the new job would otherwise find every open order in the table due at once
    /// and queue a reminder for all of them. Stamping the existing rows scopes the feature
    /// to orders placed after this deploys.
    /// </para>
    /// </remarks>
    [DbContext(typeof(ATSDBContext))]
    [Migration("20260915090000_AddFollowUpQueuedAtToEmailInvitationRequest")]
    public partial class AddFollowUpQueuedAtToEmailInvitationRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FollowUpQueuedAt",
                schema: "ats",
                table: "EmailInvitationRequest",
                type: "timestamp with time zone",
                nullable: true);

            // Every row that exists at this point predates the feature. NULL means "not
            // chased yet", so leaving them NULL would release the entire backlog on the
            // job's first pass.
            migrationBuilder.Sql(
                """
                UPDATE ats."EmailInvitationRequest"
                SET "FollowUpQueuedAt" = now()
                WHERE "FollowUpQueuedAt" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FollowUpQueuedAt",
                schema: "ats",
                table: "EmailInvitationRequest");
        }
    }
}
