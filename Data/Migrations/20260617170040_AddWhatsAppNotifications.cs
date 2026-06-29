using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentTracker.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWhatsAppNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                table: "Users",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentDueDay",
                table: "Leases",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "NotificationLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    LeaseId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ForPeriod = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    RecipientRole = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecipientPhoneNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    MessageContent = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    SentAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationLogs_Leases_LeaseId",
                        column: x => x.LeaseId,
                        principalTable: "Leases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NotificationLogs_Users_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WhatsAppSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    AccessToken = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    PhoneNumberId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    BusinessAccountId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    VerifyToken = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    EnablePaymentDueSoon = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnablePaymentToday = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnablePaymentOverdue = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnableOverdueToTenant = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnableOverdueToLender = table.Column<bool>(type: "INTEGER", nullable: false),
                    DueSoonDaysBefore = table.Column<int>(type: "INTEGER", nullable: false),
                    EnableIncomingBot = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_LeaseId",
                table: "NotificationLogs",
                column: "LeaseId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_RecipientUserId",
                table: "NotificationLogs",
                column: "RecipientUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationLogs");

            migrationBuilder.DropTable(
                name: "WhatsAppSettings");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PaymentDueDay",
                table: "Leases");
        }
    }
}
