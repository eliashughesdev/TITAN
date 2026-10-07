using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TitanMDM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGeofenceAutomationState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GeofenceDeviceStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GeofenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsInside = table.Column<bool>(type: "bit", nullable: false),
                    DistanceMeters = table.Column<double>(type: "float(18)", precision: 18, scale: 4, nullable: false),
                    LastEvaluatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastTransitionAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeofenceDeviceStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GeofenceDeviceStates_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GeofenceDeviceStates_Geofences_GeofenceId",
                        column: x => x.GeofenceId,
                        principalTable: "Geofences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GeofenceEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GeofenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Latitude = table.Column<double>(type: "float(10)", precision: 10, scale: 7, nullable: false),
                    Longitude = table.Column<double>(type: "float(10)", precision: 10, scale: 7, nullable: false),
                    DistanceMeters = table.Column<double>(type: "float(18)", precision: 18, scale: 4, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeofenceEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GeofenceEvents_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GeofenceEvents_Geofences_GeofenceId",
                        column: x => x.GeofenceId,
                        principalTable: "Geofences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GeofenceDeviceStates_DeviceId",
                table: "GeofenceDeviceStates",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_GeofenceDeviceStates_GeofenceId_DeviceId",
                table: "GeofenceDeviceStates",
                columns: new[] { "GeofenceId", "DeviceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GeofenceDeviceStates_OrganizationId_DeviceId",
                table: "GeofenceDeviceStates",
                columns: new[] { "OrganizationId", "DeviceId" });

            migrationBuilder.CreateIndex(
                name: "IX_GeofenceEvents_DeviceId",
                table: "GeofenceEvents",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_GeofenceEvents_GeofenceId_DeviceId_OccurredAtUtc",
                table: "GeofenceEvents",
                columns: new[] { "GeofenceId", "DeviceId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_GeofenceEvents_OrganizationId_OccurredAtUtc",
                table: "GeofenceEvents",
                columns: new[] { "OrganizationId", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GeofenceDeviceStates");

            migrationBuilder.DropTable(
                name: "GeofenceEvents");
        }
    }
}
