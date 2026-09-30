using ATS.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.ATS
{
    /// <summary>
    /// Makes HashTokenExpiration nullable. The application form link no longer expires.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hand-written (the EF CLI could not run in this environment); the attributes below
    /// carry what the Designer file would normally declare. The model snapshot is updated
    /// in the same commit, because the column's nullability changed.
    /// </para>
    /// <para>
    /// The column is kept rather than dropped so existing rows keep the window they were
    /// stamped with, and so this is reversible without data loss. New rows are written
    /// NULL; every guard that read it is removed in the same commit. That ordering is not
    /// optional - the old checks treated a NULL expiry as already expired, so a nullable
    /// column with any of them still in place would lock out every new candidate.
    /// </para>
    /// </remarks>
    [DbContext(typeof(ATSDBContext))]
    [Migration("20260915090100_MakeHashTokenExpirationNullable")]
    public partial class MakeHashTokenExpirationNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "HashTokenExpiration",
                schema: "ats",
                table: "EmailInvitationRequest",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rows created while the expiry was gone hold NULL and the column cannot go
            // back to NOT NULL with them in place. They are stamped with the 24-hour
            // window this reverts to, measured from the token's own creation, which is
            // exactly what the old code would have written for them.
            migrationBuilder.Sql(
                """
                UPDATE ats."EmailInvitationRequest"
                SET "HashTokenExpiration" = "HashTokenCreatedAt" + interval '24 hours'
                WHERE "HashTokenExpiration" IS NULL;
                """);

            migrationBuilder.AlterColumn<DateTime>(
                name: "HashTokenExpiration",
                schema: "ats",
                table: "EmailInvitationRequest",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }
    }
}
