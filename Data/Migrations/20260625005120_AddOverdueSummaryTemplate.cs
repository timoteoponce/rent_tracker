using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentTracker.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOverdueSummaryTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OverdueSummaryTemplateName",
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
                name: "OverdueSummaryTemplateName",
                table: "WhatsAppSettings");
        }
    }
}
