using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Features.Menus.GetMenuDishSuggestions;
using MediatR;

namespace EbedrendeloApp.Features.Menus.UpdateMenuDish;

public sealed record UpdateMenuDishCommand(
    int Id,
    string Name,
    HashSet<int> AllergenIds,
    decimal? EnergyKcal = null,
    decimal? FatGrams = null,
    decimal? SaturatedFatGrams = null,
    decimal? CarbohydrateGrams = null,
    decimal? SugarGrams = null,
    decimal? ProteinGrams = null,
    decimal? SaltGrams = null) : IRequest<Result<MenuDishDto>>;
