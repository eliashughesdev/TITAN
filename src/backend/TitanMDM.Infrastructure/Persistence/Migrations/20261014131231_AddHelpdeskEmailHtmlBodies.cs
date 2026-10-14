using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TitanMDM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHelpdeskEmailHtmlBodies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DescriptionHtml",
                table: "HelpdeskTickets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BodyHtml",
                table: "HelpdeskTicketComments",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DescriptionHtml",
                table: "HelpdeskTickets");

            migrationBuilder.DropColumn(
                name: "BodyHtml",
                table: "HelpdeskTicketComments");
        }
    }
}
