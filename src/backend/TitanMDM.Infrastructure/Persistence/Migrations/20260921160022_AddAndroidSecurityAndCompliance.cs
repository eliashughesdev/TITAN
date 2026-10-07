using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TitanMDM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAndroidSecurityAndCompliance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeviceSecurityPostures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AndroidVersion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApiLevel = table.Column<int>(type: "int", nullable: false),
                    SecurityPatchLevel = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeviceSecure = table.Column<bool>(type: "bit", nullable: false),
                    EncryptionStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AdbEnabled = table.Column<bool>(type: "bit", nullable: false),
                    DeveloperOptionsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    RootDetected = table.Column<bool>(type: "bit", nullable: false),
                    RootSignalsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EmulatorDetected = table.Column<bool>(type: "bit", nullable: false),
                    VerifiedBootState = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BootloaderLocked = table.Column<bool>(type: "bit", nullable: true),
                    SelinuxEnforced = table.Column<bool>(type: "bit", nullable: true),
                    AgentInstalled = table.Column<bool>(type: "bit", nullable: false),
                    AgentVersionName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AgentVersionCode = table.Column<long>(type: "bigint", nullable: false),
                    UnknownSourcesAllowed = table.Column<bool>(type: "bit", nullable: true),
                    ComplianceScore = table.Column<int>(type: "int", nullable: false),
                    RiskLevel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ComplianceStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalChecks = table.Column<int>(type: "int", nullable: false),
                    PassedChecks = table.Column<int>(type: "int", nullable: false),
                    FailedChecks = table.Column<int>(type: "int", nullable: false),
                    FindingsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastSecurityScanAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastComplianceCheckAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceSecurityPostures", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceSecurityPostures");
        }
    }
}
