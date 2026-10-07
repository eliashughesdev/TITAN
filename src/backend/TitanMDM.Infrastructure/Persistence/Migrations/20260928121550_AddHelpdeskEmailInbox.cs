using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TitanMDM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHelpdeskEmailInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalRequesterEmail",
                table: "HelpdeskTickets",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalRequesterName",
                table: "HelpdeskTickets",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalAuthorEmail",
                table: "HelpdeskTicketComments",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalAuthorName",
                table: "HelpdeskTicketComments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HelpdeskEmailMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Mailbox = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    InternetMessageId = table.Column<string>(type: "nvarchar(998)", maxLength: 998, nullable: false),
                    MessageKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ConversationId = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ImportedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelpdeskEmailMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HelpdeskEmailMessages_HelpdeskTickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "HelpdeskTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HelpdeskEmailMessages_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskEmailMessages_OrganizationId_Mailbox_ConversationId",
                table: "HelpdeskEmailMessages",
                columns: new[] { "OrganizationId", "Mailbox", "ConversationId" });

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskEmailMessages_OrganizationId_Mailbox_MessageKey",
                table: "HelpdeskEmailMessages",
                columns: new[] { "OrganizationId", "Mailbox", "MessageKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskEmailMessages_TicketId",
                table: "HelpdeskEmailMessages",
                column: "TicketId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HelpdeskEmailMessages");

            migrationBuilder.DropColumn(
                name: "ExternalRequesterEmail",
                table: "HelpdeskTickets");

            migrationBuilder.DropColumn(
                name: "ExternalRequesterName",
                table: "HelpdeskTickets");

            migrationBuilder.DropColumn(
                name: "ExternalAuthorEmail",
                table: "HelpdeskTicketComments");

            migrationBuilder.DropColumn(
                name: "ExternalAuthorName",
                table: "HelpdeskTicketComments");
        }
    }
}
