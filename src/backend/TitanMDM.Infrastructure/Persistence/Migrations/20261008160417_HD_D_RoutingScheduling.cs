using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TitanMDM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HD_D_RoutingScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HelpdeskTicketEvents_OrganizationId",
                table: "HelpdeskTicketEvents");

            migrationBuilder.AddColumn<int>(
                name: "RoutingAttempts",
                table: "HelpdeskTickets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "RoutingNextAttemptAtUtc",
                table: "HelpdeskTickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AssignedToUserId",
                table: "HelpdeskTicketEvents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskTickets_OrganizationId_AssigneeUserId_Status",
                table: "HelpdeskTickets",
                columns: new[] { "OrganizationId", "AssigneeUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskTickets_RoutingNextAttemptAtUtc_CreatedAtUtc",
                table: "HelpdeskTickets",
                columns: new[] { "RoutingNextAttemptAtUtc", "CreatedAtUtc" },
                filter: "[Status] <> 'closed' AND [Status] <> 'resolved' AND [Status] <> 'pendinguser'");

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskTicketEvents_OrganizationId_AssignedToUserId_EventType_CreatedAtUtc",
                table: "HelpdeskTicketEvents",
                columns: new[] { "OrganizationId", "AssignedToUserId", "EventType", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskTicketEvents_OrganizationId_TicketId_EventType_CreatedAtUtc",
                table: "HelpdeskTicketEvents",
                columns: new[] { "OrganizationId", "TicketId", "EventType", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HelpdeskTickets_OrganizationId_AssigneeUserId_Status",
                table: "HelpdeskTickets");

            migrationBuilder.DropIndex(
                name: "IX_HelpdeskTickets_RoutingNextAttemptAtUtc_CreatedAtUtc",
                table: "HelpdeskTickets");

            migrationBuilder.DropIndex(
                name: "IX_HelpdeskTicketEvents_OrganizationId_AssignedToUserId_EventType_CreatedAtUtc",
                table: "HelpdeskTicketEvents");

            migrationBuilder.DropIndex(
                name: "IX_HelpdeskTicketEvents_OrganizationId_TicketId_EventType_CreatedAtUtc",
                table: "HelpdeskTicketEvents");

            migrationBuilder.DropColumn(
                name: "RoutingAttempts",
                table: "HelpdeskTickets");

            migrationBuilder.DropColumn(
                name: "RoutingNextAttemptAtUtc",
                table: "HelpdeskTickets");

            migrationBuilder.DropColumn(
                name: "AssignedToUserId",
                table: "HelpdeskTicketEvents");

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskTicketEvents_OrganizationId",
                table: "HelpdeskTicketEvents",
                column: "OrganizationId");
        }
    }
}
