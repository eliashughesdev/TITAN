using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TitanMDM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AndroidCore01ManagedPlayCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AndroidManagedPlayApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PackageName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    ApprovalState = table.Column<int>(type: "int", nullable: false),
                    DisplayTitle = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IconUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    LatestVersionName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ProductId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    InstallationType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MetadataError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MetadataFetchedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RemovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AndroidManagedPlayApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AndroidManagedPlayApplications_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AndroidManagedPlayApplications_CreatedByUserId",
                table: "AndroidManagedPlayApplications",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AndroidManagedPlayApplications_OrganizationId_ApprovalState",
                table: "AndroidManagedPlayApplications",
                columns: new[] { "OrganizationId", "ApprovalState" });

            migrationBuilder.CreateIndex(
                name: "IX_AndroidManagedPlayApplications_OrganizationId_PackageName",
                table: "AndroidManagedPlayApplications",
                columns: new[] { "OrganizationId", "PackageName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AndroidManagedPlayApplications");
        }
    }
}
