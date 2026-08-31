using EbedrendeloApp.Common.Results;
using MediatR;

namespace EbedrendeloApp.Features.Menus.UpsertDailyMenu;

public sealed record MenuVariantInput(
    string Code,
    int SoupDishId,
    int? MainCourseDishId,
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

public sealed record UpsertDailyMenuCommand(
    DateOnly Date,
    string? Note,
    IReadOnlyList<MenuVariantInput> Variants,
    int PerformedByUserId) : IRequest<Result<int>>;
