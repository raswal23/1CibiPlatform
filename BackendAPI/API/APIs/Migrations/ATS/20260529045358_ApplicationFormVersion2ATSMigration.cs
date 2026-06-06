using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.ATS
{
    /// <inheritdoc />
    public partial class ApplicationFormVersion2ATSMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
			migrationBuilder.Sql("""
        ALTER TABLE ats."EducationalBackground"
        ALTER COLUMN "SeniorHighSchoolGraduationDate"
        TYPE timestamp with time zone
        USING NULLIF("SeniorHighSchoolGraduationDate", '')::timestamptz;
    """);

			migrationBuilder.Sql("""
        ALTER TABLE ats."EducationalBackground"
        ALTER COLUMN "MastersGraduationDate"
        TYPE timestamp with time zone
        USING NULLIF("MastersGraduationDate", '')::timestamptz;
    """);

			migrationBuilder.Sql("""
        ALTER TABLE ats."EducationalBackground"
        ALTER COLUMN "HighSchoolGraduationDate"
        TYPE timestamp with time zone
        USING NULLIF("HighSchoolGraduationDate", '')::timestamptz;
    """);

			migrationBuilder.Sql("""
        ALTER TABLE ats."EducationalBackground"
        ALTER COLUMN "DoctorateGraduationDate"
        TYPE timestamp with time zone
        USING NULLIF("DoctorateGraduationDate", '')::timestamptz;
    """);

			migrationBuilder.Sql("""
        ALTER TABLE ats."EducationalBackground"
        ALTER COLUMN "CollegeGraduationDate"
        TYPE timestamp with time zone
        USING NULLIF("CollegeGraduationDate", '')::timestamptz;
    """);

			migrationBuilder.Sql("""
        ALTER TABLE ats."EducationalBackground"
        ALTER COLUMN "BachelorsGraduationDate"
        TYPE timestamp with time zone
        USING NULLIF("BachelorsGraduationDate", '')::timestamptz;
    """);
		}

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
			migrationBuilder.Sql("""
				ALTER TABLE ats."EducationalBackground"
				ALTER COLUMN "SeniorHighSchoolGraduationDate"
				TYPE timestamp with time zone
				USING NULLIF("SeniorHighSchoolGraduationDate", '')::timestamptz;
			""");

			migrationBuilder.Sql("""
				ALTER TABLE ats."EducationalBackground"
				ALTER COLUMN "MastersGraduationDate"
				TYPE character varying(100)
				USING "MastersGraduationDate"::text;
			""");

			migrationBuilder.Sql("""
				ALTER TABLE ats."EducationalBackground"
				ALTER COLUMN "HighSchoolGraduationDate"
				TYPE character varying(100)
				USING "HighSchoolGraduationDate"::text;
			""");

			migrationBuilder.Sql("""
				ALTER TABLE ats."EducationalBackground"
				ALTER COLUMN "DoctorateGraduationDate"
				TYPE character varying(100)
				USING "DoctorateGraduationDate"::text;
			""");

			migrationBuilder.Sql("""
				ALTER TABLE ats."EducationalBackground"
				ALTER COLUMN "CollegeGraduationDate"
				TYPE character varying(100)
				USING "CollegeGraduationDate"::text;
			""");

			migrationBuilder.Sql("""
				ALTER TABLE ats."EducationalBackground"
				ALTER COLUMN "BachelorsGraduationDate"
				TYPE character varying(100)
				USING "BachelorsGraduationDate"::text;
			""");
		}
    }
}
