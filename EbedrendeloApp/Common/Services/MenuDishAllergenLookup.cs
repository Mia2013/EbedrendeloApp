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
    public static async Task<Dictionary<(MenuDishKind Kind, string Name), MenuDish>> LoadAsync(EbedrendeloDbContext db, CancellationToken cancellationToken)
    {
        var dishes = await db.MenuDishes.AsNoTracking().ToListAsync(cancellationToken);
        return dishes.ToDictionary(d => (d.Kind, d.Name));
    }
}
