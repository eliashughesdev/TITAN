using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TitanMDM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHelpdeskOutboundEmailOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HelpdeskOutboundEmails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelpdeskOutboundEmails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HelpdeskOutboundEmails_HelpdeskTicketComments_CommentId",
                        column: x => x.CommentId,
                        principalTable: "HelpdeskTicketComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HelpdeskOutboundEmails_HelpdeskTickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "HelpdeskTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HelpdeskOutboundEmails_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskOutboundEmails_CommentId",
                table: "HelpdeskOutboundEmails",
                column: "CommentId");

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskOutboundEmails_OrganizationId_CommentId",
                table: "HelpdeskOutboundEmails",
                columns: new[] { "OrganizationId", "CommentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskOutboundEmails_OrganizationId_TicketId",
                table: "HelpdeskOutboundEmails",
                columns: new[] { "OrganizationId", "TicketId" });

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskOutboundEmails_Status_NextAttemptAtUtc",
                table: "HelpdeskOutboundEmails",
                columns: new[] { "Status", "NextAttemptAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskOutboundEmails_TicketId",
                table: "HelpdeskOutboundEmails",
                column: "TicketId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HelpdeskOutboundEmails");
        }
    }
}
