using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EbedrendeloApp.Data.Migrations
{
    /// <summary>
    /// A számla a (felhasználó, időszak) pár helyett egy konkrét rendelés-halmazra szól:
    /// <c>MenuOrder.PeriodInvoiceId</c> jelöli, melyik napot melyik számla fedezi. Ezzel egy időszakra
    /// több számla is tartozhat ugyanahhoz a dolgozóhoz (alap + kiegészítő), ezért a korábbi
    /// <c>(UserId, OrderingPeriodId)</c> unique index helyére a sorszámmal bővített hármas lép.
    ///
    /// Az à la carte oszlopok kikerülnek: azt a dolgozó aznap fizeti, nem az időszaki elszámolásban.
    /// A meglévő számlák à la carte adata ezzel elveszik (a gyakorlatban mindig 0 volt, mert a számla a
    /// hónap kezdete előtt készült, à la carte pedig csak aznapra vehető).
    /// </summary>
    public partial class InvoicePerOrderSet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PeriodInvoices_UserId_OrderingPeriodId",
                table: "PeriodInvoices");

            // MenuGrossHuf == GrossHuf és MenuPayableHuf == PayableHuf lesz à la carte nélkül, ezért a
            // menü-előtagú párjuk eldobható. Fontos: ezt eldobásként és nem átnevezésként kell kezelni
            // (az EF scaffold MenuPayableHuf -> SequenceNumber átnevezést javasolt, ami pénzösszegeket
            // vinne át sorszám-oszlopba).
            migrationBuilder.DropColumn(name: "ALaCarteGrossHuf", table: "PeriodInvoices");
            migrationBuilder.DropColumn(name: "ALaCartePayableHuf", table: "PeriodInvoices");
            migrationBuilder.DropColumn(name: "MenuGrossHuf", table: "PeriodInvoices");
            migrationBuilder.DropColumn(name: "MenuPayableHuf", table: "PeriodInvoices");

            // Minden meglévő számla alapszámla — a korábbi unique index garantálta, hogy időszakonként és
            // dolgozónként csak egy van, így az egységes 1 sorszám nem sért egyediséget.
            migrationBuilder.AddColumn<int>(
                name: "SequenceNumber",
                table: "PeriodInvoices",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "PeriodInvoiceId",
                table: "MenuOrders",
                type: "int",
                nullable: true);

            // Adat-visszatöltés: a régi modellben egy számla az adott dolgozó adott időszakbeli összes
            // aktív menürendelését fedezte. Enélkül a meglévő rendelések „soha nem voltak kiszámlázva"
            // állapotba kerülnének, és a lemondásuk tévesen nem szülne jóváírást.
            migrationBuilder.Sql("""
                UPDATE o
                SET o.PeriodInvoiceId = i.Id
                FROM MenuOrders o
                INNER JOIN PeriodInvoices i
                    ON i.UserId = o.UserId AND i.OrderingPeriodId = o.OrderingPeriodId
                WHERE o.Status = 0 AND o.PeriodInvoiceId IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_PeriodInvoices_UserId_OrderingPeriodId_SequenceNumber",
                table: "PeriodInvoices",
                columns: new[] { "UserId", "OrderingPeriodId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuOrders_OrderingPeriodId_Status_PeriodInvoiceId",
                table: "MenuOrders",
                columns: new[] { "OrderingPeriodId", "Status", "PeriodInvoiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuOrders_PeriodInvoiceId",
                table: "MenuOrders",
                column: "PeriodInvoiceId");

            migrationBuilder.AddForeignKey(
                name: "FK_MenuOrders_PeriodInvoices_PeriodInvoiceId",
                table: "MenuOrders",
                column: "PeriodInvoiceId",
                principalTable: "PeriodInvoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MenuOrders_PeriodInvoices_PeriodInvoiceId",
                table: "MenuOrders");

            migrationBuilder.DropIndex(
                name: "IX_PeriodInvoices_UserId_OrderingPeriodId_SequenceNumber",
                table: "PeriodInvoices");

            migrationBuilder.DropIndex(
                name: "IX_MenuOrders_OrderingPeriodId_Status_PeriodInvoiceId",
                table: "MenuOrders");

            migrationBuilder.DropIndex(
                name: "IX_MenuOrders_PeriodInvoiceId",
                table: "MenuOrders");

            migrationBuilder.DropColumn(name: "PeriodInvoiceId", table: "MenuOrders");

            // A kiegészítő számlák (SequenceNumber > 1) miatt a régi unique index nem feltétlenül
            // állítható vissza — azokat előbb törölni kell, különben az index létrehozása elszáll.
            migrationBuilder.Sql("DELETE FROM PeriodInvoices WHERE SequenceNumber > 1;");
            migrationBuilder.DropColumn(name: "SequenceNumber", table: "PeriodInvoices");

            migrationBuilder.AddColumn<int>(
                name: "ALaCarteGrossHuf", table: "PeriodInvoices", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(
                name: "ALaCartePayableHuf", table: "PeriodInvoices", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(
                name: "MenuGrossHuf", table: "PeriodInvoices", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(
                name: "MenuPayableHuf", table: "PeriodInvoices", type: "int", nullable: false, defaultValue: 0);

            migrationBuilder.Sql("UPDATE PeriodInvoices SET MenuGrossHuf = GrossHuf, MenuPayableHuf = PayableHuf;");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodInvoices_UserId_OrderingPeriodId",
                table: "PeriodInvoices",
                columns: new[] { "UserId", "OrderingPeriodId" },
                unique: true);
        }
    }
}
