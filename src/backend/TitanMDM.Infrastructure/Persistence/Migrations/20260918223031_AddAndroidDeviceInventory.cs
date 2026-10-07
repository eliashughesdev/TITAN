using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TitanMDM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAndroidDeviceInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AndroidDevices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GoogleDeviceName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    GoogleDeviceId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ManagementMode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Ownership = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    State = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AppliedPolicyName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AppliedPolicyVersion = table.Column<long>(type: "bigint", nullable: true),
                    AppliedPolicyState = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EnrollmentTokenName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Brand = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Hardware = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DeviceBasebandVersion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BootloaderVersion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SecurityPatchLevel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ApiLevel = table.Column<int>(type: "int", nullable: true),
                    BuildNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    KernelVersion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AndroidDevicePolicyVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AndroidDevicePolicyVersionCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EncryptionStatus = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SecurityPosture = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EnrollmentTimeUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastStatusReportTimeUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastPolicySyncTimeUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSynchronizedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeletedInGoogle = table.Column<bool>(type: "bit", nullable: false),
                    DeletedInGoogleAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AndroidDevices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AndroidDevices_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AndroidDevices_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AndroidDevices_DeviceId",
                table: "AndroidDevices",
                column: "DeviceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AndroidDevices_LastStatusReportTimeUtc",
                table: "AndroidDevices",
                column: "LastStatusReportTimeUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AndroidDevices_OrganizationId_GoogleDeviceId",
                table: "AndroidDevices",
                columns: new[] { "OrganizationId", "GoogleDeviceId" });

            migrationBuilder.CreateIndex(
                name: "IX_AndroidDevices_OrganizationId_GoogleDeviceName",
                table: "AndroidDevices",
                columns: new[] { "OrganizationId", "GoogleDeviceName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AndroidDevices_OrganizationId_IsDeletedInGoogle",
                table: "AndroidDevices",
                columns: new[] { "OrganizationId", "IsDeletedInGoogle" });

            migrationBuilder.CreateIndex(
                name: "IX_AndroidDevices_OrganizationId_LastSynchronizedAtUtc",
                table: "AndroidDevices",
                columns: new[] { "OrganizationId", "LastSynchronizedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AndroidDevices_OrganizationId_ManagementMode",
                table: "AndroidDevices",
                columns: new[] { "OrganizationId", "ManagementMode" });

            migrationBuilder.CreateIndex(
                name: "IX_AndroidDevices_OrganizationId_State",
                table: "AndroidDevices",
                columns: new[] { "OrganizationId", "State" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AndroidDevices");
        }
    }
}
