using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.ATS
{
    /// <inheritdoc />
    public partial class ApplicationFormDataVersion3ATSMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "COEUpload",
                schema: "ats",
                table: "ProfessionalExperiences");

            migrationBuilder.DropColumn(
                name: "LicenseUpload",
                schema: "ats",
                table: "LicensesDetails");

            migrationBuilder.DropColumn(
                name: "SchoolSpecificLOA",
                schema: "ats",
                table: "EducationalBackground");

			migrationBuilder.Sql("""
        ALTER TABLE ats."ReferenceDetails"
        ALTER COLUMN "Ref3BestTimeToContact"
        TYPE timestamp with time zone
        USING NULLIF("Ref3BestTimeToContact", '')::timestamptz;
    """);

			migrationBuilder.Sql("""
        ALTER TABLE ats."ReferenceDetails"
        ALTER COLUMN "Ref2BestTimeToContact"
        TYPE timestamp with time zone
        USING NULLIF("Ref2BestTimeToContact", '')::timestamptz;
    """);

			migrationBuilder.Sql("""
        ALTER TABLE ats."ReferenceDetails"
        ALTER COLUMN "Ref1BestTimeToContact"
        TYPE timestamp with time zone
        USING NULLIF("Ref1BestTimeToContact", '')::timestamptz;
    """);

			migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedDate",
                schema: "ats",
                table: "ReferenceDetails",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

			migrationBuilder.Sql("""
    ALTER TABLE ats."ProfessionalExperiences"
    ALTER COLUMN "Emp3StartDate"
    TYPE date
    USING NULLIF("Emp3StartDate", '')::date;
""");

			migrationBuilder.Sql("""
    ALTER TABLE ats."ProfessionalExperiences"
    ALTER COLUMN "Emp3EndDate"
    TYPE date
    USING NULLIF("Emp3EndDate", '')::date;
""");

			migrationBuilder.Sql("""
    ALTER TABLE ats."ProfessionalExperiences"
    ALTER COLUMN "Emp2StartDate"
    TYPE date
    USING NULLIF("Emp2StartDate", '')::date;
""");

			migrationBuilder.Sql("""
    ALTER TABLE ats."ProfessionalExperiences"
    ALTER COLUMN "Emp2EndDate"
    TYPE date
    USING NULLIF("Emp2EndDate", '')::date;
""");

			migrationBuilder.Sql("""
    ALTER TABLE ats."ProfessionalExperiences"
    ALTER COLUMN "Emp1StartDate"
    TYPE date
    USING NULLIF("Emp1StartDate", '')::date;
""");

			migrationBuilder.Sql("""
    ALTER TABLE ats."ProfessionalExperiences"
    ALTER COLUMN "Emp1EndDate"
    TYPE date
    USING NULLIF("Emp1EndDate", '')::date;
""");

			migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedDate",
                schema: "ats",
                table: "ProfessionalExperiences",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "COEUploadFileKey",
                schema: "ats",
                table: "ProfessionalExperiences",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "DOB",
                schema: "ats",
                table: "PersonalDetails",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedDate",
                schema: "ats",
                table: "PersonalDetails",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

			migrationBuilder.Sql("""
    ALTER TABLE ats."LicensesDetails"
    ALTER COLUMN "LicenseExpiryDate"
    TYPE date
    USING NULLIF("LicenseExpiryDate", '')::date;
""");

			migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedDate",
                schema: "ats",
                table: "LicensesDetails",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LicenseUploadFileKey",
                schema: "ats",
                table: "LicensesDetails",
                type: "character varying(100)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<DateTime>(
                name: "HashTokenExpiration",
                schema: "ats",
                table: "EmailInvitationRequest",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "HashTokenCreated",
                schema: "ats",
                table: "EmailInvitationRequest",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "SeniorHighSchoolGraduationDate",
                schema: "ats",
                table: "EducationalBackground",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "MastersGraduationDate",
                schema: "ats",
                table: "EducationalBackground",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "HighSchoolGraduationDate",
                schema: "ats",
                table: "EducationalBackground",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "DoctorateGraduationDate",
                schema: "ats",
                table: "EducationalBackground",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedDate",
                schema: "ats",
                table: "EducationalBackground",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "CollegeGraduationDate",
                schema: "ats",
                table: "EducationalBackground",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "BachelorsGraduationDate",
                schema: "ats",
                table: "EducationalBackground",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SchoolSpecificLOAFileKey",
                schema: "ats",
                table: "EducationalBackground",
                type: "character varying(100)",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedDate",
                schema: "ats",
                table: "AddressDetails",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "COEUploadFileKey",
                schema: "ats",
                table: "ProfessionalExperiences");

            migrationBuilder.DropColumn(
                name: "LicenseUploadFileKey",
                schema: "ats",
                table: "LicensesDetails");

            migrationBuilder.DropColumn(
                name: "SchoolSpecificLOAFileKey",
                schema: "ats",
                table: "EducationalBackground");

			migrationBuilder.Sql("""
        ALTER TABLE ats."ReferenceDetails"
        ALTER COLUMN "Ref3BestTimeToContact"
        TYPE character varying(100)
        USING "Ref3BestTimeToContact"::text;
    """);

			migrationBuilder.Sql("""
        ALTER TABLE ats."ReferenceDetails"
        ALTER COLUMN "Ref2BestTimeToContact"
        TYPE character varying(100)
        USING "Ref2BestTimeToContact"::text;
    """);

			migrationBuilder.Sql("""
        ALTER TABLE ats."ReferenceDetails"
        ALTER COLUMN "Ref1BestTimeToContact"
        TYPE character varying(100)
        USING "Ref1BestTimeToContact"::text;
    """);

			migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedDate",
                schema: "ats",
                table: "ReferenceDetails",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

			migrationBuilder.Sql("""
        ALTER TABLE ats."ProfessionalExperiences"
        ALTER COLUMN "Emp3StartDate"
        TYPE character varying(100)
        USING "Emp3StartDate"::text;
    """);

			migrationBuilder.Sql("""
        ALTER TABLE ats."ProfessionalExperiences"
        ALTER COLUMN "Emp3EndDate"
        TYPE character varying(100)
        USING "Emp3EndDate"::text;
    """);

			migrationBuilder.Sql("""
        ALTER TABLE ats."ProfessionalExperiences"
        ALTER COLUMN "Emp2StartDate"
        TYPE character varying(100)
        USING "Emp2StartDate"::text;
    """);

			migrationBuilder.Sql("""
        ALTER TABLE ats."ProfessionalExperiences"
        ALTER COLUMN "Emp2EndDate"
        TYPE character varying(100)
        USING "Emp2EndDate"::text;
    """);

			migrationBuilder.Sql("""
        ALTER TABLE ats."ProfessionalExperiences"
        ALTER COLUMN "Emp1StartDate"
        TYPE character varying(100)
        USING "Emp1StartDate"::text;
    """);

			migrationBuilder.Sql("""
        ALTER TABLE ats."ProfessionalExperiences"
        ALTER COLUMN "Emp1EndDate"
        TYPE character varying(100)
        USING "Emp1EndDate"::text;
    """);

			migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedDate",
                schema: "ats",
                table: "ProfessionalExperiences",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<byte[]>(
                name: "COEUpload",
                schema: "ats",
                table: "ProfessionalExperiences",
                type: "bytea",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "DOB",
                schema: "ats",
                table: "PersonalDetails",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedDate",
                schema: "ats",
                table: "PersonalDetails",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

			migrationBuilder.Sql("""
    ALTER TABLE ats."LicensesDetails"
    ALTER COLUMN "LicenseExpiryDate"
    TYPE character varying(100)
    USING "LicenseExpiryDate"::text;
""");

			migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedDate",
                schema: "ats",
                table: "LicensesDetails",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<byte[]>(
                name: "LicenseUpload",
                schema: "ats",
                table: "LicensesDetails",
                type: "bytea",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "HashTokenExpiration",
                schema: "ats",
                table: "EmailInvitationRequest",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "HashTokenCreated",
                schema: "ats",
                table: "EmailInvitationRequest",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "SeniorHighSchoolGraduationDate",
                schema: "ats",
                table: "EducationalBackground",
                type: "timestamp with time zone",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "MastersGraduationDate",
                schema: "ats",
                table: "EducationalBackground",
                type: "timestamp with time zone",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "HighSchoolGraduationDate",
                schema: "ats",
                table: "EducationalBackground",
                type: "timestamp with time zone",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "DoctorateGraduationDate",
                schema: "ats",
                table: "EducationalBackground",
                type: "timestamp with time zone",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedDate",
                schema: "ats",
                table: "EducationalBackground",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CollegeGraduationDate",
                schema: "ats",
                table: "EducationalBackground",
                type: "timestamp with time zone",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "BachelorsGraduationDate",
                schema: "ats",
                table: "EducationalBackground",
                type: "timestamp with time zone",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "SchoolSpecificLOA",
                schema: "ats",
                table: "EducationalBackground",
                type: "bytea",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedDate",
                schema: "ats",
                table: "AddressDetails",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");
        }
    }
}
