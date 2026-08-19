using EbedrendeloApp.Domain.Enums;

namespace EbedrendeloApp.Domain.Entities;

public sealed class ALaCarteOrderLine
{
    public int Id { get; set; }
    public required int ALaCarteOrderId { get; set; }
    public ALaCarteOrder? ALaCarteOrder { get; set; }
    public required int ALaCarteDailyOfferId { get; set; }
    public required string ItemNameSnapshot { get; set; }
    public required ALaCarteCategory CategorySnapshot { get; set; }
    public required int UnitPriceHuf { get; set; }
}
