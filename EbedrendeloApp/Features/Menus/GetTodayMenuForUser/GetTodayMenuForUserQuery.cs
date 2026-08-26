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
    IReadOnlyList<MyALaCarteLineDto> MyALaCarteOrderLines);

public sealed record MyMenuSelectionDto(string VariantCode, string VariantName, int PriceHuf);

public sealed record ALaCarteOfferDto(int ALaCarteItemId, string Name, ALaCarteCategory Category, int PriceHuf, int FreeCount, string? Allergens = null);

public sealed record MyALaCarteLineDto(string ItemName, ALaCarteCategory Category, int UnitPriceHuf);
