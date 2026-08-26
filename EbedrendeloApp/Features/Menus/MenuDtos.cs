using EbedrendeloApp.Common.Allergens;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;

namespace EbedrendeloApp.Features.Menus;

public sealed record MenuVariantDto(
    string Code,
    string Name,
    string? Description,
    int SortOrder,
    string? SoupAllergens = null,
    string? MainCourseAllergens = null,
    decimal? SoupEnergyKcal = null,
    decimal? SoupFatGrams = null,
    decimal? SoupSaturatedFatGrams = null,
    decimal? SoupCarbohydrateGrams = null,
    decimal? SoupSugarGrams = null,
    decimal? SoupProteinGrams = null,
    decimal? SoupSaltGrams = null,
    decimal? MainCourseEnergyKcal = null,
    decimal? MainCourseFatGrams = null,
    decimal? MainCourseSaturatedFatGrams = null,
    decimal? MainCourseCarbohydrateGrams = null,
    decimal? MainCourseSugarGrams = null,
    decimal? MainCourseProteinGrams = null,
    decimal? MainCourseSaltGrams = null);

public sealed record DailyMenuDto(DateOnly Date, bool IsPublished, string? Note, IReadOnlyList<MenuVariantDto> Variants);

/// <summary>
/// Builds a <see cref="MenuVariantDto"/> for a persisted <see cref="MenuVariant"/>, joining its
/// soup/main-course allergens and nutrition from the dish catalog by name
/// (<see cref="EbedrendeloApp.Common.Services.MenuDishAllergenLookup"/>). Shared by every handler that
/// reads menus (GetDailyMenu, GetPeriodMenu, GetTodayMenuForUser) so the 14 nutrition fields only need
/// wiring in one place.
/// </summary>
public static class MenuVariantDtoFactory
{
    public static MenuVariantDto Create(MenuVariant variant, IReadOnlyDictionary<(MenuDishKind Kind, string Name), MenuDish> dishes)
    {
        dishes.TryGetValue((MenuDishKind.Leves, variant.Name), out var soup);
        MenuDish? mainCourse = null;
        if (variant.Description is not null)
        {
            dishes.TryGetValue((MenuDishKind.Foetel, variant.Description), out mainCourse);
        }

        return new MenuVariantDto(
            variant.Code,
            variant.Name,
            variant.Description,
            variant.SortOrder,
            soup?.Allergens,
            mainCourse?.Allergens,
            soup?.EnergyKcal,
            soup?.FatGrams,
            soup?.SaturatedFatGrams,
            soup?.CarbohydrateGrams,
            soup?.SugarGrams,
            soup?.ProteinGrams,
            soup?.SaltGrams,
            mainCourse?.EnergyKcal,
            mainCourse?.FatGrams,
            mainCourse?.SaturatedFatGrams,
            mainCourse?.CarbohydrateGrams,
            mainCourse?.SugarGrams,
            mainCourse?.ProteinGrams,
            mainCourse?.SaltGrams);
    }
}

/// <summary>
/// Renders a <see cref="MenuVariantDto"/>'s nutrition fields as a compact "En: 108, Zs: 1.8, ..." string,
/// the same format the admin enters values in — mirrors <see cref="AllergenCatalog.Format(string?)"/> for
/// display purposes. Returns <c>null</c> when every field is empty, so callers can omit the line entirely
/// instead of showing a dangling separator.
/// </summary>
public static class MenuVariantNutritionFormat
{
    public static string? FormatSoup(this MenuVariantDto variant) => Format(
        variant.SoupEnergyKcal, variant.SoupFatGrams, variant.SoupSaturatedFatGrams,
        variant.SoupCarbohydrateGrams, variant.SoupSugarGrams, variant.SoupProteinGrams, variant.SoupSaltGrams);

    public static string? FormatMainCourse(this MenuVariantDto variant) => Format(
        variant.MainCourseEnergyKcal, variant.MainCourseFatGrams, variant.MainCourseSaturatedFatGrams,
        variant.MainCourseCarbohydrateGrams, variant.MainCourseSugarGrams, variant.MainCourseProteinGrams, variant.MainCourseSaltGrams);

    public static string? Format(
        decimal? energyKcal, decimal? fatGrams, decimal? saturatedFatGrams,
        decimal? carbohydrateGrams, decimal? sugarGrams, decimal? proteinGrams, decimal? saltGrams)
    {
        List<string> parts = [];
        if (energyKcal is { } v) parts.Add($"En: {v:0.##}");
        if (fatGrams is { } v2) parts.Add($"Zs: {v2:0.##}");
        if (saturatedFatGrams is { } v3) parts.Add($"T.Zs: {v3:0.##}");
        if (carbohydrateGrams is { } v4) parts.Add($"Szh: {v4:0.##}");
        if (sugarGrams is { } v5) parts.Add($"Cuk: {v5:0.##}");
        if (proteinGrams is { } v6) parts.Add($"Feh: {v6:0.##}");
        if (saltGrams is { } v7) parts.Add($"Só: {v7:0.##}");

        return parts.Count == 0 ? null : string.Join(", ", parts);
    }
}
