using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TitanMDM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHelpdeskClosingAutomation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "FirstResponsePausedSeconds",
                table: "HelpdeskTickets",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTime>(
                name: "SlaPausedAtUtc",
                table: "HelpdeskTickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SuspendedFirstResponseDueAtUtc",
                table: "HelpdeskTickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SuspendedResolveDueAtUtc",
                table: "HelpdeskTickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "TotalSlaPausedSeconds",
                table: "HelpdeskTickets",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "HelpdeskAutomationSettings",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClassificationEnabled = table.Column<bool>(type: "bit", nullable: false),
                    EscalationDelayMinutes = table.Column<int>(type: "int", nullable: false),
                    ReopenDays = table.Column<int>(type: "int", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelpdeskAutomationSettings", x => x.OrganizationId);
                    table.ForeignKey(
                        name: "FK_HelpdeskAutomationSettings_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HelpdeskAutomationSettings");

            migrationBuilder.DropColumn(
                name: "FirstResponsePausedSeconds",
                table: "HelpdeskTickets");

            migrationBuilder.DropColumn(
                name: "SlaPausedAtUtc",
                table: "HelpdeskTickets");

            migrationBuilder.DropColumn(
                name: "SuspendedFirstResponseDueAtUtc",
                table: "HelpdeskTickets");

            migrationBuilder.DropColumn(
                name: "SuspendedResolveDueAtUtc",
                table: "HelpdeskTickets");

            migrationBuilder.DropColumn(
                name: "TotalSlaPausedSeconds",
                table: "HelpdeskTickets");
        }
    }
}
