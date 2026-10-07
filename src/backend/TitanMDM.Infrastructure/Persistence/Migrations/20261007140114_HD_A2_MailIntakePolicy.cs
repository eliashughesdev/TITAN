using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TitanMDM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HD_A2_MailIntakePolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AcceptedRecipients",
                table: "HelpdeskMailSettings",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IgnoreAutomaticMessages",
                table: "HelpdeskMailSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IgnoreBulkMessages",
                table: "HelpdeskMailSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcceptedRecipients",
                table: "HelpdeskMailSettings");

            migrationBuilder.DropColumn(
                name: "IgnoreAutomaticMessages",
                table: "HelpdeskMailSettings");

            migrationBuilder.DropColumn(
                name: "IgnoreBulkMessages",
                table: "HelpdeskMailSettings");
        }
    }
}
