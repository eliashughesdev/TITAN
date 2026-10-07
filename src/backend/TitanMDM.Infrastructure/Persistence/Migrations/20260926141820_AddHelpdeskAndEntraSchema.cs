using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TitanMDM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHelpdeskAndEntraSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EntraDirectoryUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntraObjectId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UserPrincipalName = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    Mail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    JobTitle = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Department = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    LinkedTitanUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SyncedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntraDirectoryUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntraDirectoryUsers_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EntraDirectoryUsers_Users_LinkedTitanUserId",
                        column: x => x.LinkedTitanUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "EntraIdSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ClientId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ClientSecretProtected = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AllowedGroupIds = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SyncRequestersOnly = table.Column<bool>(type: "bit", nullable: false),
                    LastSyncAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSyncStatus = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntraIdSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntraIdSettings_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HelpdeskQueues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelpdeskQueues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HelpdeskQueues_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HelpdeskTickets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RequesterUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssigneeUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    QueueId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RemoteSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EntraObjectId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    EntraUserPrincipalName = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    FirstResponseDueAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolveDueAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FirstRespondedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelpdeskTickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HelpdeskTickets_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_HelpdeskTickets_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HelpdeskTickets_Users_RequesterUserId",
                        column: x => x.RequesterUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RemoteSessionControlLeases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RemoteSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AcquiredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemoteSessionControlLeases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RemoteSessionParticipants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RemoteSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CanControl = table.Column<bool>(type: "bit", nullable: false),
                    IsConnected = table.Column<bool>(type: "bit", nullable: false),
                    JoinedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConnectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DisconnectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemoteSessionParticipants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HelpdeskTicketComments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    IsInternal = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelpdeskTicketComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HelpdeskTicketComments_HelpdeskTickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "HelpdeskTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HelpdeskTicketComments_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HelpdeskTicketEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EventType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelpdeskTicketEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HelpdeskTicketEvents_HelpdeskTickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "HelpdeskTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HelpdeskTicketEvents_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EntraDirectoryUsers_LinkedTitanUserId",
                table: "EntraDirectoryUsers",
                column: "LinkedTitanUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EntraDirectoryUsers_OrganizationId_EntraObjectId",
                table: "EntraDirectoryUsers",
                columns: new[] { "OrganizationId", "EntraObjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EntraIdSettings_OrganizationId",
                table: "EntraIdSettings",
                column: "OrganizationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskQueues_OrganizationId_Name",
                table: "HelpdeskQueues",
                columns: new[] { "OrganizationId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskTicketComments_OrganizationId",
                table: "HelpdeskTicketComments",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskTicketComments_TicketId",
                table: "HelpdeskTicketComments",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskTicketEvents_OrganizationId",
                table: "HelpdeskTicketEvents",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskTicketEvents_TicketId",
                table: "HelpdeskTicketEvents",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskTickets_DeviceId",
                table: "HelpdeskTickets",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskTickets_OrganizationId_Number",
                table: "HelpdeskTickets",
                columns: new[] { "OrganizationId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskTickets_OrganizationId_Status",
                table: "HelpdeskTickets",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskTickets_RequesterUserId",
                table: "HelpdeskTickets",
                column: "RequesterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RemoteSessionControlLeases_ExpiresAtUtc",
                table: "RemoteSessionControlLeases",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_RemoteSessionControlLeases_OrganizationId_RemoteSessionId",
                table: "RemoteSessionControlLeases",
                columns: new[] { "OrganizationId", "RemoteSessionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RemoteSessionParticipants_OrganizationId_RemoteSessionId_IsConnected",
                table: "RemoteSessionParticipants",
                columns: new[] { "OrganizationId", "RemoteSessionId", "IsConnected" });

            migrationBuilder.CreateIndex(
                name: "IX_RemoteSessionParticipants_OrganizationId_RemoteSessionId_UserId",
                table: "RemoteSessionParticipants",
                columns: new[] { "OrganizationId", "RemoteSessionId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EntraDirectoryUsers");

            migrationBuilder.DropTable(
                name: "EntraIdSettings");

            migrationBuilder.DropTable(
                name: "HelpdeskQueues");

            migrationBuilder.DropTable(
                name: "HelpdeskTicketComments");

            migrationBuilder.DropTable(
                name: "HelpdeskTicketEvents");

            migrationBuilder.DropTable(
                name: "RemoteSessionControlLeases");

            migrationBuilder.DropTable(
                name: "RemoteSessionParticipants");

            migrationBuilder.DropTable(
                name: "HelpdeskTickets");
        }
    }
}
