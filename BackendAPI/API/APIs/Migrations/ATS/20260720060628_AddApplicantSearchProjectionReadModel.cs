using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.ATS
{
    /// <inheritdoc />
    public partial class AddApplicantSearchProjectionReadModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NeedsProjection",
                schema: "ats",
                table: "EmailInvitationRequest",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProjectionUpdatedAt",
                schema: "ats",
                table: "EmailInvitationRequest",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ApplicantSearchProjection",
                schema: "ats",
                columns: table => new
                {
                    EmailInvitationRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    LastName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    MiddleInitial = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    EmailAddress = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    MobileNumber = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    SelectPackage = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    RushNormal = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    OrderStatus = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    OrderCreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OrderCompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApplicationFormStatus = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    PositionAppliedFor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    MaritalStatus = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Nationality = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Sex = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DOB = table.Column<DateOnly>(type: "date", nullable: true),
                    SSS = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    TIN = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    EmailAlternative = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CurrentAddress = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CurrentCity = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CurrentProvince = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CurrentCountry = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CurrentPostalCode = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    PermanentAddress = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    PermanentCity = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    PermanentProvince = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    PermanentCountry = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    PermanentPostalCode = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    HighestEducationalAttainment = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    BachelorsSchoolName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    BachelorsDegree = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    MastersSchoolName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    MastersDegree = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    PhDSchoolName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DoctorateDegree = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    LicenseName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    LicenseNumber = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    LicenseExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Emp1CompanyName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Emp1JobTitle = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Emp2CompanyName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Emp2JobTitle = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Emp3CompanyName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Emp3JobTitle = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Ref1FullName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Ref1ContactNumber = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Ref2FullName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Ref2ContactNumber = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Ref3FullName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Ref3ContactNumber = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    SignerName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    SignatureDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ProjectionUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicantSearchProjection", x => x.EmailInvitationRequestId);
                    table.ForeignKey(
                        name: "FK_ApplicantSearchProjection_EmailInvitationRequest_EmailInvit~",
                        column: x => x.EmailInvitationRequestId,
                        principalSchema: "ats",
                        principalTable: "EmailInvitationRequest",
                        principalColumn: "EmailInvitationID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicantSearchProjection_EmailAddress",
                schema: "ats",
                table: "ApplicantSearchProjection",
                column: "EmailAddress");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicantSearchProjection_LastName",
                schema: "ats",
                table: "ApplicantSearchProjection",
                column: "LastName");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicantSearchProjection_OrderStatus",
                schema: "ats",
                table: "ApplicantSearchProjection",
                column: "OrderStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicantSearchProjection",
                schema: "ats");

            migrationBuilder.DropColumn(
                name: "NeedsProjection",
                schema: "ats",
                table: "EmailInvitationRequest");

            migrationBuilder.DropColumn(
                name: "ProjectionUpdatedAt",
                schema: "ats",
                table: "EmailInvitationRequest");
        }
    }
}
