using EbedrendeloApp.Domain.Enums;
using MediatR;

namespace EbedrendeloApp.Features.Menus.GetTodayMenuForUser;

public sealed record GetTodayMenuForUserQuery(int UserId) : IRequest<TodayMenuDto>;

/// <summary>
/// <paramref name="MySelection"/> is null both when the day isn't orderable at all and when it is but
/// the user simply hasn't ordered yet — the caller tells the two apart via
/// <paramref name="IsOrderableToday"/> (AC 2.6.2/2.6.4: an empty field alone can't distinguish "no menu"
/// from "menu, but nothing chosen").
/// </summary>
public sealed record TodayMenuDto(
    DateOnly Date,
    bool IsOrderableToday,
    string? NotOrderableReason,
    IReadOnlyList<MenuVariantDto> Variants,
    MyMenuSelectionDto? MySelection,
    IReadOnlyList<ALaCarteOfferDto> ALaCarteOffers,
    IReadOnlyList<MyALaCarteLineDto> MyALaCarteOrderLines,
    TimeOnly ALaCarteOrderDeadlineLocalTime = default,
    bool IsALaCarteOrderableNow = false);

public sealed record MyMenuSelectionDto(string VariantCode, string VariantName, int PriceHuf);

/// <summary>Sosem tartalmaz Leves kategóriájú sort (AC 4.5.1) — a leves ára a Főétel-tételek
/// <paramref name="PriceHuf"/>-jában van benne (<paramref name="IncludesSoup"/> jelzi), önálló sorként
/// sosem jelenik meg.</summary>
public sealed record ALaCarteOfferDto(
    int ALaCarteItemId, string Name, ALaCarteCategory Category, int PriceHuf, int FreeCount,
    string? Allergens = null, bool IncludesSoup = false,
    decimal? EnergyKcal = null, decimal? FatGrams = null, decimal? SaturatedFatGrams = null,
    decimal? CarbohydrateGrams = null, decimal? SugarGrams = null, decimal? ProteinGrams = null,
    decimal? SaltGrams = null);

public sealed record MyALaCarteLineDto(int ALaCarteItemId, string ItemName, ALaCarteCategory Category, int UnitPriceHuf, bool IncludesSoup = false);
