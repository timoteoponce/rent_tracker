using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentTracker.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWhatsAppTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentDueSoonTemplateName",
                table: "WhatsAppSettings",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PaymentOverdueTemplateName",
                table: "WhatsAppSettings",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PaymentTodayTemplateName",
                table: "WhatsAppSettings",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TestTemplateName",
                table: "WhatsAppSettings",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentDueSoonTemplateName",
                table: "WhatsAppSettings");

            migrationBuilder.DropColumn(
                name: "PaymentOverdueTemplateName",
                table: "WhatsAppSettings");

            migrationBuilder.DropColumn(
                name: "PaymentTodayTemplateName",
                table: "WhatsAppSettings");

            migrationBuilder.DropColumn(
                name: "TestTemplateName",
                table: "WhatsAppSettings");
        }
    }
}
