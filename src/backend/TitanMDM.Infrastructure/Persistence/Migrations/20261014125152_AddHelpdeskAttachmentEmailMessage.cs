using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TitanMDM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHelpdeskAttachmentEmailMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EmailMessageId",
                table: "HelpdeskTicketAttachments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_HelpdeskTicketAttachments_EmailMessageId",
                table: "HelpdeskTicketAttachments",
                column: "EmailMessageId");

            migrationBuilder.AddForeignKey(
                name: "FK_HelpdeskTicketAttachments_HelpdeskEmailMessages_EmailMessageId",
                table: "HelpdeskTicketAttachments",
                column: "EmailMessageId",
                principalTable: "HelpdeskEmailMessages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HelpdeskTicketAttachments_HelpdeskEmailMessages_EmailMessageId",
                table: "HelpdeskTicketAttachments");

            migrationBuilder.DropIndex(
                name: "IX_HelpdeskTicketAttachments_EmailMessageId",
                table: "HelpdeskTicketAttachments");

            migrationBuilder.DropColumn(
                name: "EmailMessageId",
                table: "HelpdeskTicketAttachments");
        }
    }
}
