using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentTracker.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPropertyPrivacy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPrivate",
                table: "Properties",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "LastEditedById",
                table: "Properties",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Properties_LastEditedById",
                table: "Properties",
                column: "LastEditedById");

            migrationBuilder.AddForeignKey(
                name: "FK_Properties_Users_LastEditedById",
                table: "Properties",
                column: "LastEditedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Properties_Users_LastEditedById",
                table: "Properties");

            migrationBuilder.DropIndex(
                name: "IX_Properties_LastEditedById",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "IsPrivate",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "LastEditedById",
                table: "Properties");
        }
    }
}
