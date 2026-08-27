using EbedrendeloApp.Domain.Enums;

namespace EbedrendeloApp.Domain.Entities;

/// <summary>
/// A remembered soup/main-course name with its allergen list and nutritional values, keyed by
/// (Kind, Name). Not referenced by FK from <see cref="MenuVariant"/> —
/// <see cref="MenuVariant.Name"/>/<see cref="MenuVariant.Description"/> stay free text, and every read
/// (Features/Menus/Get*) re-joins to this table by name. New rows are only ever created explicitly via
/// Features/Menus/CreateMenuDish (the "+ Új étel" dialog) — but an *existing* row's allergens/nutrition
/// can still be updated in place whenever a daily menu referencing it is saved
/// (Features/Menus/UpsertDailyMenu), so a same-day correction doesn't require a separate screen.
/// </summary>
public sealed class MenuDish
{
    public int Id { get; set; }
    public required MenuDishKind Kind { get; set; }
    public required string Name { get; set; }
    public string? Allergens { get; set; }
    public decimal? EnergyKcal { get; set; }
    public decimal? FatGrams { get; set; }
    public decimal? SaturatedFatGrams { get; set; }
    public decimal? CarbohydrateGrams { get; set; }
    public decimal? SugarGrams { get; set; }
    public decimal? ProteinGrams { get; set; }
    public decimal? SaltGrams { get; set; }
}
