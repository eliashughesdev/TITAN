using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TitanMDM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AndroidCore01LauncherAckColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LauncherAppliedAtUtc",
                table: "AndroidDevices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LauncherConfigVersion",
                table: "AndroidDevices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LauncherProfileId",
                table: "AndroidDevices",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LauncherPublicationId",
                table: "AndroidDevices",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LauncherRestrictionsRead",
                table: "AndroidDevices",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LauncherSyncStatus",
                table: "AndroidDevices",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LauncherAppliedAtUtc",
                table: "AndroidDevices");

            migrationBuilder.DropColumn(
                name: "LauncherConfigVersion",
                table: "AndroidDevices");

            migrationBuilder.DropColumn(
                name: "LauncherProfileId",
                table: "AndroidDevices");

            migrationBuilder.DropColumn(
                name: "LauncherPublicationId",
                table: "AndroidDevices");

            migrationBuilder.DropColumn(
                name: "LauncherRestrictionsRead",
                table: "AndroidDevices");

            migrationBuilder.DropColumn(
                name: "LauncherSyncStatus",
                table: "AndroidDevices");
        }
    }
}
