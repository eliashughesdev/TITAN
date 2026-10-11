using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TitanMDM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WinD6InteractiveIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InteractiveSessionId",
                table: "Devices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InteractiveUser",
                table: "Devices",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InteractiveUserUpdatedAtUtc",
                table: "Devices",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InteractiveSessionId",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "InteractiveUser",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "InteractiveUserUpdatedAtUtc",
                table: "Devices");
        }
    }
}
