using ATS.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.ATS
{
    /// <summary>
    /// Adds the once-per-day guard for the package follow-up reminder, which now repeats
    /// daily instead of firing once.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hand-written (the EF CLI could not run in this environment); the attributes below
    /// carry what the Designer file would normally declare. Without them EF cannot discover
    /// the migration at all, <c>Database.MigrateAsync()</c> silently skips it, and the
    /// integration tests fail with <c>42703: column does not exist</c> against a green
    /// build. The model snapshot is updated in the same commit.
    /// </para>
    /// <para>
    /// No backfill, unlike the migration that introduced <c>FollowUpQueuedAt</c>. That one
    /// had to stamp the whole backlog because its column was the only thing standing between
    /// a brand-new job and every open order in the table. Here the replacement stop
    /// condition - the N-day window measured from <c>OrderCreatedAt</c> - already excludes
    /// historical rows: an order older than its package's reminder count is outside the
    /// window and can never be selected, whatever this column holds. Leaving it NULL is
    /// correct and means "no reminder queued yet today".
    /// </para>
    /// <para>
    /// <c>FollowUpQueuedAt</c> was left in place by this migration, still carrying the
    /// reminder-copy signal, and dropped by
    /// <c>20260917100000_DropFollowUpQueuedAtFromEmailInvitationRequest</c> once this column
    /// took that job over as well.
    /// </para>
    /// </remarks>
    [DbContext(typeof(ATSDBContext))]
    [Migration("20260916120000_AddLastFollowUpSentDateToEmailInvitationRequest")]
    public partial class AddLastFollowUpSentDateToEmailInvitationRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // "date" rather than a timestamp: the column answers "was a reminder already
            // queued today?", and the release query compares it against the current Manila
            // date. A time component would be noise the comparison has to strip every pass.
            migrationBuilder.AddColumn<DateOnly>(
                name: "LastFollowUpSentDate",
                schema: "ats",
                table: "EmailInvitationRequest",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastFollowUpSentDate",
                schema: "ats",
                table: "EmailInvitationRequest");
        }
    }
}
