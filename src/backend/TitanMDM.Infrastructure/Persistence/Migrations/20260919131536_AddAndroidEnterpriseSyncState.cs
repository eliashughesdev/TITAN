using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TitanMDM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAndroidEnterpriseSyncState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastDeviceSyncAtUtc",
                table: "AndroidEnterpriseConfigurations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastDeviceSyncCount",
                table: "AndroidEnterpriseConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LastDeviceSyncErrors",
                table: "AndroidEnterpriseConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastDeviceSyncAtUtc",
                table: "AndroidEnterpriseConfigurations");

            migrationBuilder.DropColumn(
                name: "LastDeviceSyncCount",
                table: "AndroidEnterpriseConfigurations");

            migrationBuilder.DropColumn(
                name: "LastDeviceSyncErrors",
                table: "AndroidEnterpriseConfigurations");
        }
    }
}
