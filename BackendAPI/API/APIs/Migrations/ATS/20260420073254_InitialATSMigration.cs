using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.ATS
{
    /// <inheritdoc />
    public partial class InitialATSMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ats");

            migrationBuilder.CreateTable(
                name: "EmailInvitationRequest",
                schema: "ats",
                columns: table => new
                {
                    EmailInvitationID = table.Column<Guid>(type: "uuid", nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MiddleInitial = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EmailAddress = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MobileNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SelectPackage = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RushNormal = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    HashToken = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    HashTokenCreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    HashTokenExpiration = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailInvitationRequest", x => x.EmailInvitationID);
                });

            migrationBuilder.CreateTable(
                name: "AddressDetails",
                schema: "ats",
                columns: table => new
                {
                    Address = table.Column<Guid>(type: "uuid", nullable: false),
                    EmailInvitationID = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentTypeOfOwnership = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CurrentCity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CurrentProvince = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CurrentCountry = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CurrentAddress = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CurrentPostalCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CurrentStayFrom = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PermanentTypeOfOwnership = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PermanentAddress = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PermanentCity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PermanentProvince = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PermanentCountry = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PermanentPostalCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AddressDetails", x => x.Address);
                    table.ForeignKey(
                        name: "FK_AddressDetails_EmailInvitationRequest_EmailInvitationID",
                        column: x => x.EmailInvitationID,
                        principalSchema: "ats",
                        principalTable: "EmailInvitationRequest",
                        principalColumn: "EmailInvitationID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentDetails",
                schema: "ats",
                columns: table => new
                {
                    DocumentDetailsID = table.Column<Guid>(type: "uuid", nullable: false),
                    EmailInvitationID = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DocumentValue = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentDetails", x => x.DocumentDetailsID);
                    table.ForeignKey(
                        name: "FK_DocumentDetails_EmailInvitationRequest_EmailInvitationID",
                        column: x => x.EmailInvitationID,
                        principalSchema: "ats",
                        principalTable: "EmailInvitationRequest",
                        principalColumn: "EmailInvitationID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EducationalBackground",
                schema: "ats",
                columns: table => new
                {
                    EducationalBackgroundID = table.Column<Guid>(type: "uuid", nullable: false),
                    EmailInvitationID = table.Column<Guid>(type: "uuid", nullable: false),
                    HighestEducationalAttainment = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    HighSchoolName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    HighSchoolAddress = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    HighSchoolGraduationDate = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    HighSchoolDiploma = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SeniorHighSchoolName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SeniorHighSchoolAddress = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SeniorHighSchoolGraduationDate = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SeniorHighSchoolDiploma = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CollegeSchoolName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CollegeAddress = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CollegeGraduationDate = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CollegeDiploma = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CollegeDegree = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CollegeMajor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BachelorsSchoolName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BachelorsAddress = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BachelorsGraduationDate = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BachelorsDiploma = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BachelorsDegree = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BachelorsMajor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MastersSchoolName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MastersAddress = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MastersGraduationDate = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MastersDiploma = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MastersDegree = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MastersMajor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PhDSchoolName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DoctorateAddress = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DoctorateGraduationDate = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DoctorateDiploma = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DoctorateDegree = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DoctorateMajor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SchoolSpecificLOA = table.Column<byte[]>(type: "bytea", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EducationalBackground", x => x.EducationalBackgroundID);
                    table.ForeignKey(
                        name: "FK_EducationalBackground_EmailInvitationRequest_EmailInvitatio~",
                        column: x => x.EmailInvitationID,
                        principalSchema: "ats",
                        principalTable: "EmailInvitationRequest",
                        principalColumn: "EmailInvitationID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LicensesDetails",
                schema: "ats",
                columns: table => new
                {
                    LicensesDetailsID = table.Column<Guid>(type: "uuid", nullable: false),
                    EmailInvitationID = table.Column<Guid>(type: "uuid", nullable: false),
                    LicenseName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LicenseNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LicenseExpiryDate = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LicenseUpload = table.Column<byte[]>(type: "bytea", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LicensesDetails", x => x.LicensesDetailsID);
                    table.ForeignKey(
                        name: "FK_LicensesDetails_EmailInvitationRequest_EmailInvitationID",
                        column: x => x.EmailInvitationID,
                        principalSchema: "ats",
                        principalTable: "EmailInvitationRequest",
                        principalColumn: "EmailInvitationID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PersonalDetails",
                schema: "ats",
                columns: table => new
                {
                    PersonalID = table.Column<Guid>(type: "uuid", nullable: false),
                    EmailInvitationID = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MiddleName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Suffix = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MaritalStatus = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Nationality = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Sex = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DOB = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SSS = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TIN = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MobileNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TelephoneNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EmailAddress = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EmailAlternative = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AdditionalGovtID = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    NBIClearance = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Resume = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonalDetails", x => x.PersonalID);
                    table.ForeignKey(
                        name: "FK_PersonalDetails_EmailInvitationRequest_EmailInvitationID",
                        column: x => x.EmailInvitationID,
                        principalSchema: "ats",
                        principalTable: "EmailInvitationRequest",
                        principalColumn: "EmailInvitationID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProfessionalExperiences",
                schema: "ats",
                columns: table => new
                {
                    ProfessionalExperiencesID = table.Column<Guid>(type: "uuid", nullable: false),
                    EmailInvitationID = table.Column<Guid>(type: "uuid", nullable: false),
                    Emp1CompanyName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Emp1CurrentlyEmployed = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Emp1PermissionToContact = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Emp1CompanyAddress = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Emp1StartDate = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Emp1EndDate = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Emp1JobTitle = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Emp1ReasonForLeaving = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Emp1SupervisorName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Emp1SupervisorContactNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Emp1SupervisorEmail = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Emp2CompanyName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Emp2CurrentlyEmployed = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Emp2PermissionToContact = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Emp2CompanyAddress = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Emp2StartDate = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Emp2EndDate = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Emp2JobTitle = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Emp2ReasonForLeaving = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Emp2SupervisorName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Emp2SupervisorContactNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Emp2SupervisorEmail = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Emp3CompanyName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Emp3CurrentlyEmployed = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Emp3PermissionToContact = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Emp3CompanyAddress = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Emp3StartDate = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Emp3EndDate = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Emp3JobTitle = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Emp3ReasonForLeaving = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Emp3SupervisorName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Emp3SupervisorContactNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Emp3SupervisorEmail = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    COEUpload = table.Column<byte[]>(type: "bytea", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfessionalExperiences", x => x.ProfessionalExperiencesID);
                    table.ForeignKey(
                        name: "FK_ProfessionalExperiences_EmailInvitationRequest_EmailInvitat~",
                        column: x => x.EmailInvitationID,
                        principalSchema: "ats",
                        principalTable: "EmailInvitationRequest",
                        principalColumn: "EmailInvitationID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReferenceDetails",
                schema: "ats",
                columns: table => new
                {
                    ReferenceDetailsID = table.Column<Guid>(type: "uuid", nullable: false),
                    EmailInvitationID = table.Column<Guid>(type: "uuid", nullable: false),
                    Ref1FullName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Ref1ProfessionalRelationship = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Ref1AffiliatedCompany = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Ref1Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Ref1ContactNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Ref1ModeOfContact = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Ref1BestTimeToContact = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Ref2FullName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Ref2ProfessionalRelationship = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Ref2AffiliatedCompany = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Ref2Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Ref2ContactNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Ref2ModeOfContact = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Ref2BestTimeToContact = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Ref3FullName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Ref3ProfessionalRelationship = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Ref3AffiliatedCompany = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Ref3Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Ref3ContactNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Ref3ModeOfContact = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Ref3BestTimeToContact = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferenceDetails", x => x.ReferenceDetailsID);
                    table.ForeignKey(
                        name: "FK_ReferenceDetails_EmailInvitationRequest_EmailInvitationID",
                        column: x => x.EmailInvitationID,
                        principalSchema: "ats",
                        principalTable: "EmailInvitationRequest",
                        principalColumn: "EmailInvitationID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AddressDetails_EmailInvitationID",
                schema: "ats",
                table: "AddressDetails",
                column: "EmailInvitationID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentDetails_EmailInvitationID",
                schema: "ats",
                table: "DocumentDetails",
                column: "EmailInvitationID");

            migrationBuilder.CreateIndex(
                name: "IX_EducationalBackground_EmailInvitationID",
                schema: "ats",
                table: "EducationalBackground",
                column: "EmailInvitationID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LicensesDetails_EmailInvitationID",
                schema: "ats",
                table: "LicensesDetails",
                column: "EmailInvitationID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PersonalDetails_EmailInvitationID",
                schema: "ats",
                table: "PersonalDetails",
                column: "EmailInvitationID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProfessionalExperiences_EmailInvitationID",
                schema: "ats",
                table: "ProfessionalExperiences",
                column: "EmailInvitationID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceDetails_EmailInvitationID",
                schema: "ats",
                table: "ReferenceDetails",
                column: "EmailInvitationID",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AddressDetails",
                schema: "ats");

            migrationBuilder.DropTable(
                name: "DocumentDetails",
                schema: "ats");

            migrationBuilder.DropTable(
                name: "EducationalBackground",
                schema: "ats");

            migrationBuilder.DropTable(
                name: "LicensesDetails",
                schema: "ats");

            migrationBuilder.DropTable(
                name: "PersonalDetails",
                schema: "ats");

            migrationBuilder.DropTable(
                name: "ProfessionalExperiences",
                schema: "ats");

            migrationBuilder.DropTable(
                name: "ReferenceDetails",
                schema: "ats");

            migrationBuilder.DropTable(
                name: "EmailInvitationRequest",
                schema: "ats");
        }
    }
}
