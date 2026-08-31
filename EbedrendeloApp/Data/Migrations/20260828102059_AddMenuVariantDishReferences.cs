using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EbedrendeloApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMenuVariantDishReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Name",
                table: "MenuVariants",
                newName: "SoupName");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "MenuVariants",
                newName: "MainCourseName");

            migrationBuilder.AddColumn<int>(
                name: "MainCourseDishId",
                table: "MenuVariants",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SoupDishId",
                table: "MenuVariants",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_MenuVariants_MainCourseDishId",
                table: "MenuVariants",
                column: "MainCourseDishId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuVariants_SoupDishId",
                table: "MenuVariants",
                column: "SoupDishId");

            // Best-effort backfill for existing rows: recreates the same (Kind, Name) matching the old
            // by-name join used to do at read time, but as a one-time repair instead of a runtime lookup.
            // A SoupName/MainCourseName that never had a matching MenuDishes row (the exact class of bug
            // this migration exists to fix) gets one created here — with no allergens/nutrition, same as
            // it effectively had before — so every existing MenuVariant ends up with a real FK and the
            // NOT NULL constraint on SoupDishId below can't fail.
            migrationBuilder.Sql(
                """
                INSERT INTO MenuDishes (Kind, Name)
                SELECT DISTINCT 0, mv.SoupName
                FROM MenuVariants mv
                WHERE NOT EXISTS (SELECT 1 FROM MenuDishes d WHERE d.Kind = 0 AND d.Name = mv.SoupName);

                INSERT INTO MenuDishes (Kind, Name)
                SELECT DISTINCT 1, mv.MainCourseName
                FROM MenuVariants mv
                WHERE mv.MainCourseName IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM MenuDishes d WHERE d.Kind = 1 AND d.Name = mv.MainCourseName);

                UPDATE mv
                SET mv.SoupDishId = d.Id
                FROM MenuVariants mv
                JOIN MenuDishes d ON d.Kind = 0 AND d.Name = mv.SoupName;

                UPDATE mv
                SET mv.MainCourseDishId = d.Id
                FROM MenuVariants mv
                JOIN MenuDishes d ON d.Kind = 1 AND d.Name = mv.MainCourseName
                WHERE mv.MainCourseName IS NOT NULL;
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_MenuVariants_MenuDishes_MainCourseDishId",
                table: "MenuVariants",
                column: "MainCourseDishId",
                principalTable: "MenuDishes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MenuVariants_MenuDishes_SoupDishId",
                table: "MenuVariants",
                column: "SoupDishId",
                principalTable: "MenuDishes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MenuVariants_MenuDishes_MainCourseDishId",
                table: "MenuVariants");

            migrationBuilder.DropForeignKey(
                name: "FK_MenuVariants_MenuDishes_SoupDishId",
                table: "MenuVariants");

            migrationBuilder.DropIndex(
                name: "IX_MenuVariants_MainCourseDishId",
                table: "MenuVariants");

            migrationBuilder.DropIndex(
                name: "IX_MenuVariants_SoupDishId",
                table: "MenuVariants");

            migrationBuilder.DropColumn(
                name: "MainCourseDishId",
                table: "MenuVariants");

            migrationBuilder.DropColumn(
                name: "SoupDishId",
                table: "MenuVariants");

            migrationBuilder.RenameColumn(
                name: "SoupName",
                table: "MenuVariants",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "MainCourseName",
                table: "MenuVariants",
                newName: "Description");
        }
    }
}
