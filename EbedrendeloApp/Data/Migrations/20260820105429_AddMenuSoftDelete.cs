using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EbedrendeloApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMenuSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RemovedAtUtc",
                table: "MenuVariants",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RemovedAtUtc",
                table: "DailyMenus",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RemovedAtUtc",
                table: "MenuVariants");

            migrationBuilder.DropColumn(
                name: "RemovedAtUtc",
                table: "DailyMenus");
        }
    }
}
