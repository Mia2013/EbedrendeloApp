using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EbedrendeloApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIgazgatosagOsztaly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Igazgatosag",
                table: "Users",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Osztaly",
                table: "Users",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Igazgatosag",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Osztaly",
                table: "Users");
        }
    }
}
