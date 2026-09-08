namespace EbedrendeloApp.Features.Kitchen;

public sealed record KitchenVariantLineDto(string VariantCode, string VariantName, int Quantity);

/// <summary>Élő összesítő egy napra (US-6.1, AC 6.1.1/6.1.3) — csak <c>Active</c> rendelést számol,
/// mindig a jelenlegi variáns-adatokból épül, nem egy korábbi zárás pillanatképéből.</summary>
public sealed record KitchenSummaryDto(DateOnly Date, bool IsClosed, IReadOnlyList<KitchenVariantLineDto> Lines, int TotalPortions);

public sealed record KitchenClosureLineDto(string VariantCode, string VariantNameSnapshot, int Quantity);

/// <summary>A nap legutóbbi zárási pillanatképe (US-6.3). <see cref="ReopenedAtUtc"/> jelzi, ha ez a
/// zárás azóta fel lett oldva — a snapshot (<see cref="Lines"/>, <see cref="TotalPortions"/>) ettől
/// függetlenül változatlan marad (AC 6.3.2).</summary>
public sealed record KitchenClosureDto(
    DateOnly Date,
    DateTime ClosedAtUtc,
    int ClosedByUserId,
    string ClosedByUserName,
    int TotalPortions,
    IReadOnlyList<KitchenClosureLineDto> Lines,
    DateTime? ReopenedAtUtc,
    int? ReopenedByUserId,
    string? ReopenedByUserName);
