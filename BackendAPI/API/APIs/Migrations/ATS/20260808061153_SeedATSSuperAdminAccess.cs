using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.ATS
{
    /// <inheritdoc />
    public partial class SeedATSSuperAdminAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "ClientId",
                schema: "ats",
                table: "UserDetails",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.Sql(
                """
                INSERT INTO ats."ModuleDetails"
                    ("ModuleId", "ModuleName", "ModuleDescription", "IsActive", "CreatedAt", "UpdatedAt")
                VALUES
                    (1, 'Dashboard', 'Dashboard', TRUE, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
                    (2, 'New Order', 'New Order', TRUE, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
                    (3, 'Orders & Reports', 'Orders & Reports', TRUE, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
                    (4, 'Disputes', 'Disputes', TRUE, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
                    (5, 'Withdrawn', 'Withdrawn', TRUE, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
                    (6, 'Package Management', 'Package Management', TRUE, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
                    (7, 'Client Management', 'Client Management', TRUE, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
                    (8, 'Role Management', 'Role Management', TRUE, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
                    (9, 'Module Management', 'Module Management', TRUE, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
                    (10, 'User Management', 'User Management', TRUE, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
                ON CONFLICT ("ModuleId") DO UPDATE SET
                    "ModuleName" = EXCLUDED."ModuleName",
                    "ModuleDescription" = EXCLUDED."ModuleDescription",
                    "IsActive" = TRUE,
                    "UpdatedAt" = CURRENT_TIMESTAMP;

                SELECT setval(
                    pg_get_serial_sequence('ats."ModuleDetails"', 'ModuleId'),
                    GREATEST((SELECT MAX("ModuleId") FROM ats."ModuleDetails"), 1),
                    TRUE);

                INSERT INTO ats."UserDetails"
                    ("UserId", "UserName", "UserEmail", "IsActive", "ClientId", "Site",
                     "RoleId", "ModuleId", "CreatedAt", "UpdatedAt")
                SELECT
                    auth_user."Id",
                    CONCAT_WS(' ', auth_user."FirstName", NULLIF(auth_user."MiddleName", ''), auth_user."LastName"),
                    auth_user."Email",
                    TRUE,
                    NULL,
                    'All',
                    ats_role."RoleId",
                    ats_module."ModuleId",
                    CURRENT_TIMESTAMP,
                    CURRENT_TIMESTAMP
                FROM "AuthUsers" AS auth_user
                CROSS JOIN LATERAL (
                    SELECT role."RoleId"
                    FROM ats."RoleDetails" AS role
                    ORDER BY
                        CASE WHEN LOWER(role."RoleName") = 'superadmin' THEN 0 ELSE 1 END,
                        role."RoleId"
                    LIMIT 1
                ) AS ats_role
                INNER JOIN ats."ModuleDetails" AS ats_module
                    ON ats_module."ModuleId" BETWEEN 1 AND 10
                WHERE LOWER(auth_user."Email") = 'admin@cibi.com'
                ON CONFLICT ("UserId", "ModuleId") DO UPDATE SET
                    "UserName" = EXCLUDED."UserName",
                    "UserEmail" = EXCLUDED."UserEmail",
                    "IsActive" = TRUE,
                    "ClientId" = NULL,
                    "Site" = EXCLUDED."Site",
                    "RoleId" = EXCLUDED."RoleId",
                    "UpdatedAt" = CURRENT_TIMESTAMP;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM ats."UserDetails" AS ats_user
                USING "AuthUsers" AS auth_user
                WHERE ats_user."UserId" = auth_user."Id"
                  AND LOWER(auth_user."Email") = 'admin@cibi.com'
                  AND ats_user."ModuleId" BETWEEN 1 AND 10
                  AND ats_user."ClientId" IS NULL;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "ClientId",
                schema: "ats",
                table: "UserDetails",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
