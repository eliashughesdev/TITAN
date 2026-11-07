using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TitanMDM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEnrollmentTokenAndroidPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AndroidPolicyId",
                table: "EnrollmentTokens",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AndroidPolicyId",
                table: "EnrollmentTokens");
        }
    }
}
