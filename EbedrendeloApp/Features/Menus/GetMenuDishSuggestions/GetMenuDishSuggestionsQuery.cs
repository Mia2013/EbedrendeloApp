using EbedrendeloApp.Domain.Enums;
using MediatR;

namespace EbedrendeloApp.Features.Menus.GetMenuDishSuggestions;

public sealed record GetMenuDishSuggestionsQuery : IRequest<MenuDishSuggestionsDto>;

public sealed record MenuDishDto(
    string Name,
    string? Allergens,
    decimal? EnergyKcal = null,
    decimal? FatGrams = null,
    decimal? SaturatedFatGrams = null,
    decimal? CarbohydrateGrams = null,
    decimal? SugarGrams = null,
    decimal? ProteinGrams = null,
    decimal? SaltGrams = null,
    int? Id = null,
    MenuDishKind Kind = MenuDishKind.Leves);

public sealed record MenuDishSuggestionsDto(IReadOnlyList<MenuDishDto> Soups, IReadOnlyList<MenuDishDto> MainCourses);
