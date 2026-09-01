using EbedrendeloApp.Domain.Enums;

namespace EbedrendeloApp.Domain.Entities;

public sealed class ALaCarteOrderLine
{
    public int Id { get; set; }
    public required int ALaCarteOrderId { get; set; }
    public ALaCarteOrder? ALaCarteOrder { get; set; }
    public required int ALaCarteDailyOfferId { get; set; }
    public ALaCarteDailyOffer? ALaCarteDailyOffer { get; set; }
    public required string ItemNameSnapshot { get; set; }
    public required ALaCarteCategory CategorySnapshot { get; set; }
    public required int UnitPriceHuf { get; set; }

    /// <summary>Snapshot, nem élő állapotból számolt — igaz, ha ez a sor Főétel és a rendelés
    /// pillanatában volt aznapra aktív Leves-ajánlat (annak ára a fenti UnitPriceHuf-ba be van
    /// olvasztva). Az admin utólag módosíthatja/törölheti az aznapi Leves-ajánlatot vagy a főétel
    /// árát, ezért ez sosem számolható újra élőben — a UI ebből dönti el a "(levessel)" jelzést.</summary>
    public bool IncludesSoup { get; set; }
}
