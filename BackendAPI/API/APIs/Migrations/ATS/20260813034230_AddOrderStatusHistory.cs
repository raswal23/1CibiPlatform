using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIs.Migrations.ATS
{
    /// <inheritdoc />
    public partial class AddOrderStatusHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrderStatusHistory",
                schema: "ats",
                columns: table => new
                {
                    OrderStatusHistoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmailInvitationRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PreviousStatus = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    NewStatus = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Source = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderStatusHistory", x => x.OrderStatusHistoryId);
                    table.ForeignKey(
                        name: "FK_OrderStatusHistory_EmailInvitationRequest_EmailInvitationRe~",
                        column: x => x.EmailInvitationRequestId,
                        principalSchema: "ats",
                        principalTable: "EmailInvitationRequest",
                        principalColumn: "EmailInvitationID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderStatusHistory_EmailInvitationRequestId_OccurredAt",
                schema: "ats",
                table: "OrderStatusHistory",
                columns: new[] { "EmailInvitationRequestId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderStatusHistory",
                schema: "ats");
        }
    }
}
