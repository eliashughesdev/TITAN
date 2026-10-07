using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TitanMDM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAndroidEnterpriseSignupSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AndroidEnterpriseSignupSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    State = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SignupUrlName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AndroidEnterpriseSignupSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AndroidEnterpriseSignupSessions_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AndroidEnterpriseSignupSessions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AndroidEnterpriseSignupSessions_CreatedByUserId",
                table: "AndroidEnterpriseSignupSessions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AndroidEnterpriseSignupSessions_ExpiresAtUtc_CompletedAtUtc",
                table: "AndroidEnterpriseSignupSessions",
                columns: new[] { "ExpiresAtUtc", "CompletedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AndroidEnterpriseSignupSessions_OrganizationId_CreatedAtUtc",
                table: "AndroidEnterpriseSignupSessions",
                columns: new[] { "OrganizationId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AndroidEnterpriseSignupSessions_State",
                table: "AndroidEnterpriseSignupSessions",
                column: "State",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AndroidEnterpriseSignupSessions");
        }
    }
}
