using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Domain.Enums;
using MediatR;

namespace EbedrendeloApp.Features.ALaCarte.UpsertALaCarteItem;

/// <summary><paramref name="Id"/> null → létrehozás, egyébként teljes felülírás (nem "üres mező = nincs
/// változás" — a szerkesztő dialógus mindig a jelenlegi állapotot mutatja, mint a MenuDish szerkesztőnél).</summary>
public sealed record UpsertALaCarteItemCommand(
    int? Id,
    string Name,
    ALaCarteCategory Category,
    int PriceHuf,
    bool IsActive,
    HashSet<int> AllergenIds,
    decimal? EnergyKcal = null,
    decimal? FatGrams = null,
    decimal? SaturatedFatGrams = null,
    decimal? CarbohydrateGrams = null,
    decimal? SugarGrams = null,
    decimal? ProteinGrams = null,
    decimal? SaltGrams = null) : IRequest<Result<ALaCarteItemDto>>;
