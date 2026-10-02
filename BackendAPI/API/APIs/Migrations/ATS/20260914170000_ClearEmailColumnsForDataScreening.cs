using ATS.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.ATS
{
    /// <summary>
    /// Makes EmailSentStatus nullable and clears every email column on data-screening
    /// orders, which are never sent an application form.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hand-written (the EF CLI could not run in this environment); the attributes below
    /// carry what the Designer file would normally declare. The model snapshot is updated
    /// in the same commit, because the column's nullability changed.
    /// </para>
    /// <para>
    /// NULL means "no email applies to this order", which is a different statement from
    /// Pending ("queued, not sent yet"). Data orders had no such value available before,
    /// so they were stamped Pending and counted as invitations still in flight on every
    /// dashboard - a queue position they never actually held, since the email worker
    /// claims "AutoChasing" IS TRUE and would never advance them.
    /// </para>
    /// </remarks>
    [DbContext(typeof(ATSDBContext))]
    [Migration("20260914170000_ClearEmailColumnsForDataScreening")]
    public partial class ClearEmailColumnsForDataScreening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "EmailSentStatus",
                schema: "ats",
                table: "EmailInvitationRequest",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            // Orders placed before the send was suppressed: the email went out, so the row
            // carries a Done status and a send timestamp for a form that should never have
            // been requested. Clearing all four columns together keeps them consistent -
            // a NULL status beside a populated EmailSentAt would read as corruption.
            //
            // Only AutoChasing IS FALSE. NULL is excluded deliberately: 20260914160000
            // classified every legacy row as manual, so anything still NULL here arrived
            // after that and cannot prove it is a data order.
            migrationBuilder.Sql(
                """
                UPDATE ats."EmailInvitationRequest"
                SET "EmailSentStatus" = NULL,
                    "EmailSentAt" = NULL,
                    "EmailClaimedAt" = NULL,
                    "EmailSendAttempts" = 0
                WHERE "AutoChasing" IS FALSE;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The column cannot go back to NOT NULL while data orders hold NULL, and the
            // cleared values are not recoverable, so reverting restores the old shape by
            // stamping those rows Pending - the value they would have had before this
            // migration. Their AutoChasing keeps them out of the email worker's claim
            // either way, so this is a labelling change, not a resurrection of the send.
            migrationBuilder.Sql(
                """
                UPDATE ats."EmailInvitationRequest"
                SET "EmailSentStatus" = 'Pending'
                WHERE "EmailSentStatus" IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "EmailSentStatus",
                schema: "ats",
                table: "EmailInvitationRequest",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);
        }
    }
}
