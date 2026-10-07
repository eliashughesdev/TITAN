using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TitanMDM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHelpdeskRoutingAndAssistantAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HelpdeskAssistantAccess",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GrantedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    GrantedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelpdeskAssistantAccess", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HelpdeskAssistantAccess_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HelpdeskAssistantAccess_Users_GrantedByUserId",
                        column: x => x.GrantedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HelpdeskAssistantAccess_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HelpdeskTeams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelpdeskTeams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HelpdeskTeams_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HelpdeskZones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentZoneId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelpdeskZones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HelpdeskZones_HelpdeskZones_ParentZoneId",
                        column: x => x.ParentZoneId,
                        principalTable: "HelpdeskZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HelpdeskZones_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HelpdeskTeamMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcceptsAutomaticAssignments = table.Column<bool>(type: "bit", nullable: false),
                    IsAvailable = table.Column<bool>(type: "bit", nullable: false),
                    MaxOpenTickets = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelpdeskTeamMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HelpdeskTeamMembers_HelpdeskTeams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "HelpdeskTeams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HelpdeskTeamMembers_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HelpdeskTeamMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HelpdeskTeamZones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ZoneId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelpdeskTeamZones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HelpdeskTeamZones_HelpdeskTeams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "HelpdeskTeams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HelpdeskTeamZones_HelpdeskZones_ZoneId",
                        column: x => x.ZoneId,
                        principalTable: "HelpdeskZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HelpdeskTeamZones_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HelpdeskUserZones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ZoneId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelpdeskUserZones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HelpdeskUserZones_HelpdeskZones_ZoneId",
                        column: x => x.ZoneId,
                        principalTable: "HelpdeskZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HelpdeskUserZones_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HelpdeskUserZones_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskAssistantAccess_GrantedByUserId",
                table: "HelpdeskAssistantAccess",
                column: "GrantedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskAssistantAccess_OrganizationId_UserId",
                table: "HelpdeskAssistantAccess",
                columns: new[] { "OrganizationId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskAssistantAccess_UserId",
                table: "HelpdeskAssistantAccess",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskTeamMembers_OrganizationId_AcceptsAutomaticAssignments_IsAvailable",
                table: "HelpdeskTeamMembers",
                columns: new[] { "OrganizationId", "AcceptsAutomaticAssignments", "IsAvailable" });

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskTeamMembers_OrganizationId_TeamId_UserId",
                table: "HelpdeskTeamMembers",
                columns: new[] { "OrganizationId", "TeamId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskTeamMembers_TeamId",
                table: "HelpdeskTeamMembers",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskTeamMembers_UserId",
                table: "HelpdeskTeamMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskTeams_OrganizationId_Name",
                table: "HelpdeskTeams",
                columns: new[] { "OrganizationId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskTeamZones_OrganizationId_TeamId_ZoneId",
                table: "HelpdeskTeamZones",
                columns: new[] { "OrganizationId", "TeamId", "ZoneId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskTeamZones_TeamId",
                table: "HelpdeskTeamZones",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskTeamZones_ZoneId",
                table: "HelpdeskTeamZones",
                column: "ZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskUserZones_OrganizationId_UserId_ZoneId",
                table: "HelpdeskUserZones",
                columns: new[] { "OrganizationId", "UserId", "ZoneId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskUserZones_UserId",
                table: "HelpdeskUserZones",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskUserZones_ZoneId",
                table: "HelpdeskUserZones",
                column: "ZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskZones_OrganizationId_ParentZoneId_Name",
                table: "HelpdeskZones",
                columns: new[] { "OrganizationId", "ParentZoneId", "Name" },
                unique: true,
                filter: "[ParentZoneId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskZones_OrganizationId_Type_IsActive",
                table: "HelpdeskZones",
                columns: new[] { "OrganizationId", "Type", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskZones_ParentZoneId",
                table: "HelpdeskZones",
                column: "ParentZoneId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HelpdeskAssistantAccess");

            migrationBuilder.DropTable(
                name: "HelpdeskTeamMembers");

            migrationBuilder.DropTable(
                name: "HelpdeskTeamZones");

            migrationBuilder.DropTable(
                name: "HelpdeskUserZones");

            migrationBuilder.DropTable(
                name: "HelpdeskTeams");

            migrationBuilder.DropTable(
                name: "HelpdeskZones");
        }
    }
}
