using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TitanMDM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceApplicationInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeviceApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PackageName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ApplicationName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    VersionName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    VersionCode = table.Column<long>(type: "bigint", nullable: false),
                    IsSystemApp = table.Column<bool>(type: "bit", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    IsPresent = table.Column<bool>(type: "bit", nullable: false),
                    FirstInstallTimeUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdateTimeUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InstallerPackageName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FirstSeenAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceApplications_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeviceApplications_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceApplications_ApplicationName",
                table: "DeviceApplications",
                column: "ApplicationName");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceApplications_DeviceId_PackageName",
                table: "DeviceApplications",
                columns: new[] { "DeviceId", "PackageName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceApplications_OrganizationId_IsPresent",
                table: "DeviceApplications",
                columns: new[] { "OrganizationId", "IsPresent" });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceApplications_OrganizationId_PackageName",
                table: "DeviceApplications",
                columns: new[] { "OrganizationId", "PackageName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceApplications");
        }
    }
}
