using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.ATS
{
    /// <inheritdoc />
    public partial class AddedSignatureDetailsEntityATSMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhilSysImageKey",
                schema: "ats",
                table: "PersonalDetails",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SignatureDetails",
                schema: "ats",
                columns: table => new
                {
                    SignatureDetailsID = table.Column<Guid>(type: "uuid", nullable: false),
                    EmailInvitationID = table.Column<Guid>(type: "uuid", nullable: false),
                    SignatureFileKey = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Signature = table.Column<string>(type: "text", nullable: true),
                    SignerName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SignatureDate = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignatureDetails", x => x.SignatureDetailsID);
                    table.ForeignKey(
                        name: "FK_SignatureDetails_EmailInvitationRequest_EmailInvitationID",
                        column: x => x.EmailInvitationID,
                        principalSchema: "ats",
                        principalTable: "EmailInvitationRequest",
                        principalColumn: "EmailInvitationID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SignatureDetails_EmailInvitationID",
                schema: "ats",
                table: "SignatureDetails",
                column: "EmailInvitationID",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SignatureDetails",
                schema: "ats");

            migrationBuilder.DropColumn(
                name: "PhilSysImageKey",
                schema: "ats",
                table: "PersonalDetails");
        }
    }
}
