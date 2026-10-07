using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TitanMDM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAndroidEnterpriseCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AndroidEnrollments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    GoogleEnrollmentTokenName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsRevoked = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AndroidEnrollments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AndroidEnrollments_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AndroidEnrollments_Policies_PolicyId",
                        column: x => x.PolicyId,
                        principalTable: "Policies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AndroidEnrollments_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AndroidEnterpriseConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GoogleProjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EnterpriseName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    EnterpriseDisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConnectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AndroidEnterpriseConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AndroidEnterpriseConfigurations_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AndroidEnrollments_CreatedByUserId",
                table: "AndroidEnrollments",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AndroidEnrollments_GoogleEnrollmentTokenName",
                table: "AndroidEnrollments",
                column: "GoogleEnrollmentTokenName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AndroidEnrollments_OrganizationId_CreatedAtUtc",
                table: "AndroidEnrollments",
                columns: new[] { "OrganizationId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AndroidEnrollments_OrganizationId_IsRevoked_ExpiresAtUtc",
                table: "AndroidEnrollments",
                columns: new[] { "OrganizationId", "IsRevoked", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AndroidEnrollments_PolicyId",
                table: "AndroidEnrollments",
                column: "PolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_AndroidEnterpriseConfigurations_EnterpriseName",
                table: "AndroidEnterpriseConfigurations",
                column: "EnterpriseName",
                unique: true,
                filter: "[EnterpriseName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AndroidEnterpriseConfigurations_OrganizationId",
                table: "AndroidEnterpriseConfigurations",
                column: "OrganizationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AndroidEnrollments");

            migrationBuilder.DropTable(
                name: "AndroidEnterpriseConfigurations");
        }
    }
}
