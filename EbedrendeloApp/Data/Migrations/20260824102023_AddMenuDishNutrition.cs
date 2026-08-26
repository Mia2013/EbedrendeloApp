using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EbedrendeloApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMenuDishNutrition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CarbohydrateGrams",
                table: "MenuDishes",
                type: "decimal(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EnergyKcal",
                table: "MenuDishes",
                type: "decimal(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FatGrams",
                table: "MenuDishes",
                type: "decimal(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ProteinGrams",
                table: "MenuDishes",
                type: "decimal(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SaltGrams",
                table: "MenuDishes",
                type: "decimal(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SaturatedFatGrams",
                table: "MenuDishes",
                type: "decimal(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SugarGrams",
                table: "MenuDishes",
                type: "decimal(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CarbohydrateGrams",
                table: "MenuDishes");

            migrationBuilder.DropColumn(
                name: "EnergyKcal",
                table: "MenuDishes");

            migrationBuilder.DropColumn(
                name: "FatGrams",
                table: "MenuDishes");

            migrationBuilder.DropColumn(
                name: "ProteinGrams",
                table: "MenuDishes");

            migrationBuilder.DropColumn(
                name: "SaltGrams",
                table: "MenuDishes");

            migrationBuilder.DropColumn(
                name: "SaturatedFatGrams",
                table: "MenuDishes");

            migrationBuilder.DropColumn(
                name: "SugarGrams",
                table: "MenuDishes");
        }
    }
}
