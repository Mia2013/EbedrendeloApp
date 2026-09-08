using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EbedrendeloApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddKitchenClosureReopening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_KitchenClosures_Date",
                table: "KitchenClosures");

            migrationBuilder.CreateTable(
                name: "KitchenClosureReopenings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KitchenClosureId = table.Column<int>(type: "int", nullable: false),
                    ReopenedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReopenedByUserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KitchenClosureReopenings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KitchenClosureReopenings_KitchenClosures_KitchenClosureId",
                        column: x => x.KitchenClosureId,
                        principalTable: "KitchenClosures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KitchenClosureReopenings_Users_ReopenedByUserId",
                        column: x => x.ReopenedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KitchenClosures_Date",
                table: "KitchenClosures",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_KitchenClosureReopenings_KitchenClosureId",
                table: "KitchenClosureReopenings",
                column: "KitchenClosureId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KitchenClosureReopenings_ReopenedByUserId",
                table: "KitchenClosureReopenings",
                column: "ReopenedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KitchenClosureReopenings");

            migrationBuilder.DropIndex(
                name: "IX_KitchenClosures_Date",
                table: "KitchenClosures");

            migrationBuilder.CreateIndex(
                name: "IX_KitchenClosures_Date",
                table: "KitchenClosures",
                column: "Date",
                unique: true);
        }
    }
}
