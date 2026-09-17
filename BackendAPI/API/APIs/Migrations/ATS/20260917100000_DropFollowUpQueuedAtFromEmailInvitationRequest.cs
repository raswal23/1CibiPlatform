using ATS.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.ATS
{
    /// <summary>
    /// Drops <c>FollowUpQueuedAt</c>. <c>LastFollowUpSentDate</c> now serves both of its jobs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hand-written (the EF CLI could not run in this environment); the attributes below carry
    /// what the Designer file would normally declare. Without them EF cannot discover the
    /// migration at all, <c>Database.MigrateAsync()</c> silently skips it, and the column stays
    /// behind against a green build. The model snapshot is updated in the same commit.
    /// </para>
    /// <para>
    /// The column was introduced as the fire-once flag. When reminders became daily that role
    /// passed to <c>LastFollowUpSentDate</c>, leaving it read by exactly one line - the sender's
    /// choice between reminder and first-invitation copy - which the date column answers just as
    /// well.
    /// </para>
    /// <para>
    /// Removing it also fixes a real defect rather than only deleting a column. Its own migration
    /// backfilled every pre-existing row with <c>now()</c> to protect the backlog from the
    /// then-new chaser. That backfill made the copy check lie: an operator resending a legacy
    /// order clears <c>EmailSentAt</c>, and with a non-null stamp already present the row read as
    /// "queued as a follow-up", so a candidate who had never been chased received an email
    /// telling them we had not yet received their form. <c>LastFollowUpSentDate</c> has no
    /// backfill, so null genuinely means never chased.
    /// </para>
    /// <para>
    /// Down re-adds the column as nullable and does NOT restore the backfill, so a rollback lands
    /// on the corrected state rather than reintroducing the defect above. Nothing reads the
    /// column at that point either way.
    /// </para>
    /// </remarks>
    [DbContext(typeof(ATSDBContext))]
    [Migration("20260917100000_DropFollowUpQueuedAtFromEmailInvitationRequest")]
    public partial class DropFollowUpQueuedAtFromEmailInvitationRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FollowUpQueuedAt",
                schema: "ats",
                table: "EmailInvitationRequest");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FollowUpQueuedAt",
                schema: "ats",
                table: "EmailInvitationRequest",
                type: "timestamp with time zone",
                nullable: true);
        }
    }
}
