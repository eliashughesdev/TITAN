using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TitanMDM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiSiteAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_LockedUntilUtc",
                table: "Users");

            migrationBuilder.AlterColumn<int>(
                name: "SecurityVersion",
                table: "Users",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 1);

            migrationBuilder.AlterColumn<int>(
                name: "FailedLoginAttempts",
                table: "Users",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "SiteId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SiteLocationId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SiteId",
                table: "HelpdeskTickets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SiteLocationId",
                table: "HelpdeskTickets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SiteId",
                table: "Devices",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SiteLocationId",
                table: "Devices",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_OrganizationId_SiteId",
                table: "Users",
                columns: new[] { "OrganizationId", "SiteId" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_OrganizationId_SiteLocationId",
                table: "Users",
                columns: new[] { "OrganizationId", "SiteLocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_SiteId",
                table: "Users",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_SiteLocationId",
                table: "Users",
                column: "SiteLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskTickets_OrganizationId_SiteId",
                table: "HelpdeskTickets",
                columns: new[] { "OrganizationId", "SiteId" });

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskTickets_OrganizationId_SiteLocationId",
                table: "HelpdeskTickets",
                columns: new[] { "OrganizationId", "SiteLocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskTickets_SiteId",
                table: "HelpdeskTickets",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskTickets_SiteLocationId",
                table: "HelpdeskTickets",
                column: "SiteLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_OrganizationId_SiteId",
                table: "Devices",
                columns: new[] { "OrganizationId", "SiteId" });

            migrationBuilder.CreateIndex(
                name: "IX_Devices_OrganizationId_SiteLocationId",
                table: "Devices",
                columns: new[] { "OrganizationId", "SiteLocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_Devices_SiteId",
                table: "Devices",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_SiteLocationId",
                table: "Devices",
                column: "SiteLocationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Devices_SiteLocations_SiteLocationId",
                table: "Devices",
                column: "SiteLocationId",
                principalTable: "SiteLocations",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Devices_Sites_SiteId",
                table: "Devices",
                column: "SiteId",
                principalTable: "Sites",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_HelpdeskTickets_SiteLocations_SiteLocationId",
                table: "HelpdeskTickets",
                column: "SiteLocationId",
                principalTable: "SiteLocations",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_HelpdeskTickets_Sites_SiteId",
                table: "HelpdeskTickets",
                column: "SiteId",
                principalTable: "Sites",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_SiteLocations_SiteLocationId",
                table: "Users",
                column: "SiteLocationId",
                principalTable: "SiteLocations",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Sites_SiteId",
                table: "Users",
                column: "SiteId",
                principalTable: "Sites",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Devices_SiteLocations_SiteLocationId",
                table: "Devices");

            migrationBuilder.DropForeignKey(
                name: "FK_Devices_Sites_SiteId",
                table: "Devices");

            migrationBuilder.DropForeignKey(
                name: "FK_HelpdeskTickets_SiteLocations_SiteLocationId",
                table: "HelpdeskTickets");

            migrationBuilder.DropForeignKey(
                name: "FK_HelpdeskTickets_Sites_SiteId",
                table: "HelpdeskTickets");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_SiteLocations_SiteLocationId",
                table: "Users");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Sites_SiteId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_OrganizationId_SiteId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_OrganizationId_SiteLocationId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_SiteId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_SiteLocationId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_HelpdeskTickets_OrganizationId_SiteId",
                table: "HelpdeskTickets");

            migrationBuilder.DropIndex(
                name: "IX_HelpdeskTickets_OrganizationId_SiteLocationId",
                table: "HelpdeskTickets");

            migrationBuilder.DropIndex(
                name: "IX_HelpdeskTickets_SiteId",
                table: "HelpdeskTickets");

            migrationBuilder.DropIndex(
                name: "IX_HelpdeskTickets_SiteLocationId",
                table: "HelpdeskTickets");

            migrationBuilder.DropIndex(
                name: "IX_Devices_OrganizationId_SiteId",
                table: "Devices");

            migrationBuilder.DropIndex(
                name: "IX_Devices_OrganizationId_SiteLocationId",
                table: "Devices");

            migrationBuilder.DropIndex(
                name: "IX_Devices_SiteId",
                table: "Devices");

            migrationBuilder.DropIndex(
                name: "IX_Devices_SiteLocationId",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "SiteId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SiteLocationId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SiteId",
                table: "HelpdeskTickets");

            migrationBuilder.DropColumn(
                name: "SiteLocationId",
                table: "HelpdeskTickets");

            migrationBuilder.DropColumn(
                name: "SiteId",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "SiteLocationId",
                table: "Devices");

            migrationBuilder.AlterColumn<int>(
                name: "SecurityVersion",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "FailedLoginAttempts",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateIndex(
                name: "IX_Users_LockedUntilUtc",
                table: "Users",
                column: "LockedUntilUtc");
        }
    }
}
