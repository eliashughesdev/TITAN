using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TitanMDM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLocationReportingPoliciesAndDeviceLocationStates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ConsecutiveFailures",
                table: "AutomationRules",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DefinitionJson",
                table: "AutomationRules",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedByUserId",
                table: "AutomationRules",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Module",
                table: "AutomationRules",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "AutomationRules",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ScopeSiteIdsJson",
                table: "AutomationRules",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ActorUserId",
                table: "AutomationExecutions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CycleDepth",
                table: "AutomationExecutions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "DurationMs",
                table: "AutomationExecutions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "AutomationExecutions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "Simulated",
                table: "AutomationExecutions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AutomationNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AutomationRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AutomationExecutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TargetUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TargetRole = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReadAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomationNotifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AutomationQueueItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AutomationRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AutomationExecutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TriggerType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TriggerPayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    State = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    MaxAttempts = table.Column<int>(type: "int", nullable: false),
                    CycleDepth = table.Column<int>(type: "int", nullable: false),
                    Simulated = table.Column<bool>(type: "bit", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LeaseUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomationQueueItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AutomationSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AutomationRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Recurrence = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IntervalMinutes = table.Column<int>(type: "int", nullable: true),
                    TimeOfDay = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    DayOfWeek = table.Column<int>(type: "int", nullable: true),
                    DayOfMonth = table.Column<int>(type: "int", nullable: true),
                    TimeZoneId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StartAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextRunAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastRunAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CatchUp = table.Column<bool>(type: "bit", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomationSchedules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeviceLocationStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaptureState = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceLocationStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceLocationStates_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LocationReportingPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    ReportingIntervalMinutes = table.Column<int>(type: "int", nullable: false),
                    RetentionDays = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationReportingPolicies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationNotifications_OrganizationId_IsRead_CreatedAtUtc",
                table: "AutomationNotifications",
                columns: new[] { "OrganizationId", "IsRead", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationQueueItems_IdempotencyKey",
                table: "AutomationQueueItems",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AutomationQueueItems_OrganizationId_State",
                table: "AutomationQueueItems",
                columns: new[] { "OrganizationId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationQueueItems_State_NextAttemptAtUtc",
                table: "AutomationQueueItems",
                columns: new[] { "State", "NextAttemptAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationSchedules_NextRunAtUtc",
                table: "AutomationSchedules",
                column: "NextRunAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AutomationSchedules_OrganizationId_IsEnabled_NextRunAtUtc",
                table: "AutomationSchedules",
                columns: new[] { "OrganizationId", "IsEnabled", "NextRunAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceLocationStates_DeviceId",
                table: "DeviceLocationStates",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceLocationStates_OrganizationId_DeviceId",
                table: "DeviceLocationStates",
                columns: new[] { "OrganizationId", "DeviceId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AutomationNotifications");

            migrationBuilder.DropTable(
                name: "AutomationQueueItems");

            migrationBuilder.DropTable(
                name: "AutomationSchedules");

            migrationBuilder.DropTable(
                name: "DeviceLocationStates");

            migrationBuilder.DropTable(
                name: "LocationReportingPolicies");

            migrationBuilder.DropColumn(
                name: "ConsecutiveFailures",
                table: "AutomationRules");

            migrationBuilder.DropColumn(
                name: "DefinitionJson",
                table: "AutomationRules");

            migrationBuilder.DropColumn(
                name: "ModifiedByUserId",
                table: "AutomationRules");

            migrationBuilder.DropColumn(
                name: "Module",
                table: "AutomationRules");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "AutomationRules");

            migrationBuilder.DropColumn(
                name: "ScopeSiteIdsJson",
                table: "AutomationRules");

            migrationBuilder.DropColumn(
                name: "ActorUserId",
                table: "AutomationExecutions");

            migrationBuilder.DropColumn(
                name: "CycleDepth",
                table: "AutomationExecutions");

            migrationBuilder.DropColumn(
                name: "DurationMs",
                table: "AutomationExecutions");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "AutomationExecutions");

            migrationBuilder.DropColumn(
                name: "Simulated",
                table: "AutomationExecutions");
        }
    }
}
