using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TitanMDM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HD_A3_EnterpriseMailIntake : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "AcceptedRecipients",
                table: "HelpdeskMailSettings",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AllowedDomains",
                table: "HelpdeskMailSettings",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AllowedSenders",
                table: "HelpdeskMailSettings",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BlockedDomains",
                table: "HelpdeskMailSettings",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BlockedSenders",
                table: "HelpdeskMailSettings",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IgnoreBounceMessages",
                table: "HelpdeskMailSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IgnoreNoReplyMessages",
                table: "HelpdeskMailSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "IgnoredSubjectPatterns",
                table: "HelpdeskMailSettings",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HelpdeskMailProcessingLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Mailbox = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    InternetMessageId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ConversationId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FromEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Decision = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelpdeskMailProcessingLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HelpdeskMailProcessingLogs_HelpdeskTickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "HelpdeskTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_HelpdeskMailProcessingLogs_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskMailProcessingLogs_OrganizationId_Decision",
                table: "HelpdeskMailProcessingLogs",
                columns: new[] { "OrganizationId", "Decision" });

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskMailProcessingLogs_OrganizationId_Mailbox_InternetMessageId",
                table: "HelpdeskMailProcessingLogs",
                columns: new[] { "OrganizationId", "Mailbox", "InternetMessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskMailProcessingLogs_OrganizationId_ProcessedAtUtc",
                table: "HelpdeskMailProcessingLogs",
                columns: new[] { "OrganizationId", "ProcessedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskMailProcessingLogs_TicketId",
                table: "HelpdeskMailProcessingLogs",
                column: "TicketId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HelpdeskMailProcessingLogs");

            migrationBuilder.DropColumn(
                name: "AllowedDomains",
                table: "HelpdeskMailSettings");

            migrationBuilder.DropColumn(
                name: "AllowedSenders",
                table: "HelpdeskMailSettings");

            migrationBuilder.DropColumn(
                name: "BlockedDomains",
                table: "HelpdeskMailSettings");

            migrationBuilder.DropColumn(
                name: "BlockedSenders",
                table: "HelpdeskMailSettings");

            migrationBuilder.DropColumn(
                name: "IgnoreBounceMessages",
                table: "HelpdeskMailSettings");

            migrationBuilder.DropColumn(
                name: "IgnoreNoReplyMessages",
                table: "HelpdeskMailSettings");

            migrationBuilder.DropColumn(
                name: "IgnoredSubjectPatterns",
                table: "HelpdeskMailSettings");

            migrationBuilder.AlterColumn<string>(
                name: "AcceptedRecipients",
                table: "HelpdeskMailSettings",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000,
                oldNullable: true);
        }
    }
}
