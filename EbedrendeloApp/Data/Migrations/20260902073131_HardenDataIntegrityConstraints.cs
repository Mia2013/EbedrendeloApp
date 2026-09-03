using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EbedrendeloApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class HardenDataIntegrityConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_MenuVariant_Code",
                table: "MenuVariants",
                sql: "[Code] IN ('A', 'B', 'C')");

            migrationBuilder.CreateIndex(
                name: "IX_ALaCarteItems_Category_Name",
                table: "ALaCarteItems",
                columns: new[] { "Category", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MenuVariant_Code",
                table: "MenuVariants");

            migrationBuilder.DropIndex(
                name: "IX_ALaCarteItems_Category_Name",
                table: "ALaCarteItems");
        }
    }
}
