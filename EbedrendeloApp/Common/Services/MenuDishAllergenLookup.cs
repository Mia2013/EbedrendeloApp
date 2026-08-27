using EbedrendeloApp.Data;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Common.Services;

/// <summary>
/// Loads the whole <see cref="Domain.Entities.MenuDish"/> catalog into a (Kind, Name) -&gt; dish lookup
/// (allergens + nutrition). The catalog is small (one row per distinct dish ever served), so a full load
/// per query beats a per-row join — same reasoning as <c>ExcludeDayDialog</c>'s reason autocomplete.
/// </summary>
public static class MenuDishAllergenLookup
{
    /// <summary>
    /// Case-insensitive (Kind, Name) key comparer, matching MenuDishConfiguration's unique index on
    /// (Kind, Name) under SQL Server's default (case-insensitive) collation. MenuVariant.Name/Description
    /// are denormalized free text, not FK-linked to MenuDish (see MenuDish.cs) — so a case-only rename via
    /// UpdateMenuDishHandler (which only guards against a case-SENSITIVE self-conflict) must not strand
    /// already-saved MenuVariant rows that still carry the pre-rename casing: an ordinal lookup would miss
    /// them and silently drop allergen/nutrition data from every future menu read.
    /// </summary>
    public static readonly IEqualityComparer<(MenuDishKind Kind, string Name)> KeyComparer = new DishKeyComparer();

    public static async Task<Dictionary<(MenuDishKind Kind, string Name), MenuDish>> LoadAsync(EbedrendeloDbContext db, CancellationToken cancellationToken)
    {
        var dishes = await db.MenuDishes.AsNoTracking().ToListAsync(cancellationToken);
        return dishes.ToDictionary(d => (d.Kind, d.Name), KeyComparer);
    }

    private sealed class DishKeyComparer : IEqualityComparer<(MenuDishKind Kind, string Name)>
    {
        public bool Equals((MenuDishKind Kind, string Name) x, (MenuDishKind Kind, string Name) y)
            => x.Kind == y.Kind && string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((MenuDishKind Kind, string Name) obj)
            => HashCode.Combine(obj.Kind, obj.Name.ToUpperInvariant());
    }
}
