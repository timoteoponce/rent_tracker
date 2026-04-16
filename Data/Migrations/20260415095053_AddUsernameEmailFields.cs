using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentTracker.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUsernameEmailFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_FullName",
                table: "Users");

            // Add columns as nullable initially (will populate data first)
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Users",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSystemUser",
                table: "Users",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "Users",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            // Populate data: Set admin user
            migrationBuilder.Sql(@"
                UPDATE Users 
                SET Username = 'admin',
                    Email = 'admin@fakemail.ch',
                    IsSystemUser = 1
                WHERE FullName = 'admin' 
                   OR (Role = 'Administrator' AND (Username IS NULL OR Username = ''));
            ");

            // Populate data: Set remaining users with random unique values
            migrationBuilder.Sql(@"
                UPDATE Users 
                SET Username = 'user-' || lower(hex(randomblob(4))),
                    Email = 'user-' || lower(hex(randomblob(4))) || '@fakemail.ch',
                    IsSystemUser = 0
                WHERE Username IS NULL OR Username = '';
            ");

            // Alter columns to be non-nullable
            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<bool>(
                name: "IsSystemUser",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "Username",
                table: "Users",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_Username",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsSystemUser",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Username",
                table: "Users");

            migrationBuilder.CreateIndex(
                name: "IX_Users_FullName",
                table: "Users",
                column: "FullName",
                unique: true);
        }
    }
}
