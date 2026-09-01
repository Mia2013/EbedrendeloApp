using EbedrendeloApp.Domain.Enums;

namespace EbedrendeloApp.Features.ALaCarte;

public sealed record ALaCarteItemDto(
    int Id,
    string Name,
    ALaCarteCategory Category,
    int PriceHuf,
    bool IsActive,
    string? Allergens,
    decimal? EnergyKcal,
    decimal? FatGrams,
    decimal? SaturatedFatGrams,
    decimal? CarbohydrateGrams,
    decimal? SugarGrams,
    decimal? ProteinGrams,
    decimal? SaltGrams);

/// <summary>Leves kategóriánál <paramref name="Capacity"/> mindig <see cref="int.MaxValue"/> (a keret
/// rá nézve figyelmen kívül hagyott — AC 4.1.4/4.2.4) — a UI ezt nem is jeleníti meg leves-sornál.</summary>
public sealed record ALaCarteDailyOfferDto(
    int OfferId,
    DateOnly Date,
    int ALaCarteItemId,
    string ItemName,
    ALaCarteCategory Category,
    int ItemPriceHuf,
    int Capacity,
    int OrderedCount,
    int FreeCount);

public sealed record PlacedALaCarteOrderLineDto(int ALaCarteItemId, string ItemName, ALaCarteCategory Category, int UnitPriceHuf, bool IncludesSoup);

public sealed record PlacedALaCarteOrderLinesDto(IReadOnlyList<PlacedALaCarteOrderLineDto> Lines, int TotalHuf);
