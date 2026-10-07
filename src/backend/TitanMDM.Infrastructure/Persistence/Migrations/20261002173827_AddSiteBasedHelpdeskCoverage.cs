using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TitanMDM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSiteBasedHelpdeskCoverage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HelpdeskSiteCoverages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SiteLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelpdeskSiteCoverages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HelpdeskSiteCoverages_HelpdeskTeams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "HelpdeskTeams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HelpdeskSiteCoverages_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HelpdeskSiteCoverages_SiteLocations_SiteLocationId",
                        column: x => x.SiteLocationId,
                        principalTable: "SiteLocations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_HelpdeskSiteCoverages_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskSiteCoverages_OrganizationId_SiteId_SiteLocationId_Category",
                table: "HelpdeskSiteCoverages",
                columns: new[] { "OrganizationId", "SiteId", "SiteLocationId", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskSiteCoverages_OrganizationId_TeamId",
                table: "HelpdeskSiteCoverages",
                columns: new[] { "OrganizationId", "TeamId" });

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskSiteCoverages_OrganizationId_TeamId_SiteId_SiteLocationId_Category",
                table: "HelpdeskSiteCoverages",
                columns: new[] { "OrganizationId", "TeamId", "SiteId", "SiteLocationId", "Category" },
                unique: true,
                filter: "[SiteLocationId] IS NOT NULL AND [Category] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskSiteCoverages_SiteId",
                table: "HelpdeskSiteCoverages",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskSiteCoverages_SiteLocationId",
                table: "HelpdeskSiteCoverages",
                column: "SiteLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskSiteCoverages_TeamId",
                table: "HelpdeskSiteCoverages",
                column: "TeamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HelpdeskSiteCoverages");
        }
    }
}
