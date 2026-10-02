using ATS.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.ATS
{
    /// <summary>
    /// Adds the reminder counter that becomes the follow-up schedule's stop condition, replacing
    /// the purely time-based window.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hand-written (the EF CLI could not run in this environment); the attributes below carry
    /// what the Designer file would normally declare. Without them EF cannot discover the
    /// migration at all, <c>Database.MigrateAsync()</c> silently skips it, and the integration
    /// tests fail with <c>42703: column does not exist</c> against a green build. The model
    /// snapshot is updated in the same commit.
    /// </para>
    /// <para>
    /// Why the column exists: the schedule used to end <c>FollowUpEmail</c> days after the
    /// order, which silently assumed a reminder goes out every day. It does not - a row is only
    /// released when it satisfies every rule in the release query, so a day passes with nothing
    /// sent whenever the first invitation is still queued behind the send quota or has failed.
    /// Candidates who received the least were therefore chased the fewest times, and the board's
    /// "Follow-ups Left" counted down for people who had received no email at all.
    /// </para>
    /// <para>
    /// A counter rather than deriving the number sent from
    /// <c>LastFollowUpSentDate - OrderCreatedAt</c>: that gap only equals the count while every
    /// reminder lands on its own day, and overcounts as soon as a missed one is caught up late.
    /// </para>
    /// <para>
    /// No backfill, matching <c>20260916120000_AddLastFollowUpSentDateToEmailInvitationRequest</c>.
    /// 0 is the correct reading for every existing row - none of them have been counted - and the
    /// backlog stays protected by the release query's grace window
    /// (<c>ATSRepository.FollowUpCatchUpGraceDays</c>), which keeps anything well past its
    /// schedule permanently ineligible regardless of this value.
    /// </para>
    /// </remarks>
    [DbContext(typeof(ATSDBContext))]
    [Migration("20260918090000_AddFollowUpSentCountToEmailInvitationRequest")]
    public partial class AddFollowUpSentCountToEmailInvitationRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NOT NULL with a 0 default: "no reminders sent" is a known quantity, not unknown,
            // and the default is what lets existing rows read correctly without a backfill.
            migrationBuilder.AddColumn<int>(
                name: "FollowUpSentCount",
                schema: "ats",
                table: "EmailInvitationRequest",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FollowUpSentCount",
                schema: "ats",
                table: "EmailInvitationRequest");
        }
    }
}
