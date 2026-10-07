using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TitanMDM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAndroidPolicyPublications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AndroidPolicyPublications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyVersion = table.Column<int>(type: "int", nullable: false),
                    GooglePolicyId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    GooglePolicyName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CompiledPolicyJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GoogleResponseJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ErrorCode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AndroidPolicyPublications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AndroidPolicyPublications_Policies_PolicyId",
                        column: x => x.PolicyId,
                        principalTable: "Policies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AndroidPolicyPublications_PolicyVersions_PolicyVersionId",
                        column: x => x.PolicyVersionId,
                        principalTable: "PolicyVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AndroidPolicyPublications_OrganizationId_GooglePolicyId",
                table: "AndroidPolicyPublications",
                columns: new[] { "OrganizationId", "GooglePolicyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AndroidPolicyPublications_OrganizationId_PolicyId_PolicyVersion",
                table: "AndroidPolicyPublications",
                columns: new[] { "OrganizationId", "PolicyId", "PolicyVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AndroidPolicyPublications_OrganizationId_Status",
                table: "AndroidPolicyPublications",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AndroidPolicyPublications_PolicyId",
                table: "AndroidPolicyPublications",
                column: "PolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_AndroidPolicyPublications_PolicyVersionId",
                table: "AndroidPolicyPublications",
                column: "PolicyVersionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AndroidPolicyPublications");
        }
    }
}
