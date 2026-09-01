using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EbedrendeloApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddALaCarteItemNutritionAndIncludesSoup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IncludesSoup",
                table: "ALaCarteOrderLines",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "CarbohydrateGrams",
                table: "ALaCarteItems",
                type: "decimal(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EnergyKcal",
                table: "ALaCarteItems",
                type: "decimal(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FatGrams",
                table: "ALaCarteItems",
                type: "decimal(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ProteinGrams",
                table: "ALaCarteItems",
                type: "decimal(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SaltGrams",
                table: "ALaCarteItems",
                type: "decimal(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SaturatedFatGrams",
                table: "ALaCarteItems",
                type: "decimal(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SugarGrams",
                table: "ALaCarteItems",
                type: "decimal(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IncludesSoup",
                table: "ALaCarteOrderLines");

            migrationBuilder.DropColumn(
                name: "CarbohydrateGrams",
                table: "ALaCarteItems");

            migrationBuilder.DropColumn(
                name: "EnergyKcal",
                table: "ALaCarteItems");

            migrationBuilder.DropColumn(
                name: "FatGrams",
                table: "ALaCarteItems");

            migrationBuilder.DropColumn(
                name: "ProteinGrams",
                table: "ALaCarteItems");

            migrationBuilder.DropColumn(
                name: "SaltGrams",
                table: "ALaCarteItems");

            migrationBuilder.DropColumn(
                name: "SaturatedFatGrams",
                table: "ALaCarteItems");

            migrationBuilder.DropColumn(
                name: "SugarGrams",
                table: "ALaCarteItems");
        }
    }
}
