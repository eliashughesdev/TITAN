using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TitanMDM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRemoteSupportSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RemoteSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TechnicianName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AllowKeyboard = table.Column<bool>(type: "bit", nullable: false),
                    AllowMouse = table.Column<bool>(type: "bit", nullable: false),
                    AllowClipboard = table.Column<bool>(type: "bit", nullable: false),
                    AllowFileTransfer = table.Column<bool>(type: "bit", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConnectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DisconnectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FailureReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TerminationReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TerminatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemoteSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RemoteSessions_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RemoteSessions_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RemoteSessions_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RemoteSessionEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RemoteSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemoteSessionEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RemoteSessionEvents_RemoteSessions_RemoteSessionId",
                        column: x => x.RemoteSessionId,
                        principalTable: "RemoteSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RemoteSessionEvents_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_RemoteSessionEvents_OrganizationId_OccurredAtUtc",
                table: "RemoteSessionEvents",
                columns: new[] { "OrganizationId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RemoteSessionEvents_RemoteSessionId_OccurredAtUtc",
                table: "RemoteSessionEvents",
                columns: new[] { "RemoteSessionId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RemoteSessionEvents_UserId",
                table: "RemoteSessionEvents",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RemoteSessions_DeviceId_Status",
                table: "RemoteSessions",
                columns: new[] { "DeviceId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RemoteSessions_OrganizationId_Status",
                table: "RemoteSessions",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RemoteSessions_RequestedAtUtc",
                table: "RemoteSessions",
                column: "RequestedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_RemoteSessions_RequestedByUserId",
                table: "RemoteSessions",
                column: "RequestedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RemoteSessionEvents");

            migrationBuilder.DropTable(
                name: "RemoteSessions");
        }
    }
}
