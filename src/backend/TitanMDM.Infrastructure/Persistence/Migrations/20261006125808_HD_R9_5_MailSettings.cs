using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TitanMDM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HD_R9_5_MailSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HelpdeskMailSettings",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Mailbox = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InboundEnabled = table.Column<bool>(type: "bit", nullable: false),
                    OutboundEnabled = table.Column<bool>(type: "bit", nullable: false),
                    InboundPollSeconds = table.Column<int>(type: "int", nullable: false),
                    OutboundPollSeconds = table.Column<int>(type: "int", nullable: false),
                    BatchSize = table.Column<int>(type: "int", nullable: false),
                    MaxAttempts = table.Column<int>(type: "int", nullable: false),
                    LastInboundAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastInboundSuccessAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastInboundError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    LastOutboundAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastOutboundSuccessAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastOutboundError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelpdeskMailSettings", x => x.OrganizationId);
                    table.ForeignKey(
                        name: "FK_HelpdeskMailSettings_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskMailSettings_ActorUserId",
                table: "HelpdeskMailSettings",
                column: "ActorUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HelpdeskMailSettings");
        }
    }
}
