using ATS.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.ATS
{
    /// <summary>
    /// Adds the cleanup bookkeeping flag to bulk upload files. The daily 1 AM
    /// <c>BulkUploadFileCleanupJob</c> deletes a processed file's CSV from object
    /// storage and needs a durable marker that it already did so, because clearing
    /// <c>FileKey</c> alone cannot distinguish "already deleted" from "never
    /// uploaded". Defaults to false so every existing row stays eligible.
    /// </summary>
    /// <remarks>
    /// Hand-written (the EF CLI could not run in this environment); the attributes
    /// below carry what the Designer file would normally declare, and the model
    /// snapshot was updated in the same commit.
    /// </remarks>
    [DbContext(typeof(ATSDBContext))]
    [Migration("20260916120000_AddIsFileKeyDeletedToBulkUploadFileDetailsATSMigration")]
    public partial class AddIsFileKeyDeletedToBulkUploadFileDetailsATSMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsFileKeyDeleted",
                schema: "ats",
                table: "BulkUploadFileDetails",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsFileKeyDeleted",
                schema: "ats",
                table: "BulkUploadFileDetails");
        }
    }
}
