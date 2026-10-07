using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TitanMDM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HD_A5_EmailAttachmentsAndSignatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContentId",
                table: "HelpdeskTicketAttachments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsInline",
                table: "HelpdeskTicketAttachments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskTicketAttachments_OrganizationId_TicketId_IsInline",
                table: "HelpdeskTicketAttachments",
                columns: new[] { "OrganizationId", "TicketId", "IsInline" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HelpdeskTicketAttachments_OrganizationId_TicketId_IsInline",
                table: "HelpdeskTicketAttachments");

            migrationBuilder.DropColumn(
                name: "ContentId",
                table: "HelpdeskTicketAttachments");

            migrationBuilder.DropColumn(
                name: "IsInline",
                table: "HelpdeskTicketAttachments");
        }
    }
}
