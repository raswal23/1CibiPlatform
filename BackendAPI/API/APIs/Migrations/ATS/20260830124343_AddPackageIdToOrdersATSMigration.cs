using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.ATS
{
    /// <inheritdoc />
    public partial class AddPackageIdToOrdersATSMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PackageId",
                schema: "ats",
                table: "EmailInvitationRequest",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PackageId",
                schema: "ats",
                table: "BulkUploadFileDetails",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_EmailInvitationRequest_PackageId",
                schema: "ats",
                table: "EmailInvitationRequest",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_BulkUploadFileDetails_PackageId",
                schema: "ats",
                table: "BulkUploadFileDetails",
                column: "PackageId");

            migrationBuilder.AddForeignKey(
                name: "FK_BulkUploadFileDetails_PackageDetails_PackageId",
                schema: "ats",
                table: "BulkUploadFileDetails",
                column: "PackageId",
                principalSchema: "ats",
                principalTable: "PackageDetails",
                principalColumn: "PackageId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EmailInvitationRequest_PackageDetails_PackageId",
                schema: "ats",
                table: "EmailInvitationRequest",
                column: "PackageId",
                principalSchema: "ats",
                principalTable: "PackageDetails",
                principalColumn: "PackageId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BulkUploadFileDetails_PackageDetails_PackageId",
                schema: "ats",
                table: "BulkUploadFileDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_EmailInvitationRequest_PackageDetails_PackageId",
                schema: "ats",
                table: "EmailInvitationRequest");

            migrationBuilder.DropIndex(
                name: "IX_EmailInvitationRequest_PackageId",
                schema: "ats",
                table: "EmailInvitationRequest");

            migrationBuilder.DropIndex(
                name: "IX_BulkUploadFileDetails_PackageId",
                schema: "ats",
                table: "BulkUploadFileDetails");

            migrationBuilder.DropColumn(
                name: "PackageId",
                schema: "ats",
                table: "EmailInvitationRequest");

            migrationBuilder.DropColumn(
                name: "PackageId",
                schema: "ats",
                table: "BulkUploadFileDetails");
        }
    }
}
