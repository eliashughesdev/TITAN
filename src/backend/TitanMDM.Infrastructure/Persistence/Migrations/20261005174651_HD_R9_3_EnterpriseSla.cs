using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TitanMDM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HD_R9_3_EnterpriseSla : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CriticalFirstResponseMinutes",
                table: "HelpdeskAutomationSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CriticalResolutionMinutes",
                table: "HelpdeskAutomationSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "HighFirstResponseMinutes",
                table: "HelpdeskAutomationSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "HighResolutionMinutes",
                table: "HelpdeskAutomationSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LowFirstResponseMinutes",
                table: "HelpdeskAutomationSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LowResolutionMinutes",
                table: "HelpdeskAutomationSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MediumFirstResponseMinutes",
                table: "HelpdeskAutomationSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MediumResolutionMinutes",
                table: "HelpdeskAutomationSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "PauseSlaWhenWaitingUser",
                table: "HelpdeskAutomationSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CriticalFirstResponseMinutes",
                table: "HelpdeskAutomationSettings");

            migrationBuilder.DropColumn(
                name: "CriticalResolutionMinutes",
                table: "HelpdeskAutomationSettings");

            migrationBuilder.DropColumn(
                name: "HighFirstResponseMinutes",
                table: "HelpdeskAutomationSettings");

            migrationBuilder.DropColumn(
                name: "HighResolutionMinutes",
                table: "HelpdeskAutomationSettings");

            migrationBuilder.DropColumn(
                name: "LowFirstResponseMinutes",
                table: "HelpdeskAutomationSettings");

            migrationBuilder.DropColumn(
                name: "LowResolutionMinutes",
                table: "HelpdeskAutomationSettings");

            migrationBuilder.DropColumn(
                name: "MediumFirstResponseMinutes",
                table: "HelpdeskAutomationSettings");

            migrationBuilder.DropColumn(
                name: "MediumResolutionMinutes",
                table: "HelpdeskAutomationSettings");

            migrationBuilder.DropColumn(
                name: "PauseSlaWhenWaitingUser",
                table: "HelpdeskAutomationSettings");
        }
    }
}
