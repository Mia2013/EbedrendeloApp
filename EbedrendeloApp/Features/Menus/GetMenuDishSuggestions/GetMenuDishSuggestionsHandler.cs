using EbedrendeloApp.Data;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.Menus.GetMenuDishSuggestions;

public sealed class GetMenuDishSuggestionsHandler(IDbContextFactory<EbedrendeloDbContext> dbFactory)
    : IRequestHandler<GetMenuDishSuggestionsQuery, MenuDishSuggestionsDto>
{
    public async Task<MenuDishSuggestionsDto> Handle(GetMenuDishSuggestionsQuery request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Plain OrderBy (no StringComparer) — this runs server-side as SQL, which can't translate a
        // .NET StringComparer; ordering by SQL Server's default collation is fine for a suggestion list.
        var dishes = await db.MenuDishes.OrderBy(d => d.Name).ToListAsync(cancellationToken);

        var soups = dishes.Where(d => d.Kind == MenuDishKind.Leves).Select(ToDto).ToList();
        var mainCourses = dishes.Where(d => d.Kind == MenuDishKind.Foetel).Select(ToDto).ToList();

        return new MenuDishSuggestionsDto(soups, mainCourses);
    }

    private static MenuDishDto ToDto(MenuDish d) => new(
        d.Name,
        d.Allergens,
        d.EnergyKcal,
        d.FatGrams,
        d.SaturatedFatGrams,
        d.CarbohydrateGrams,
        d.SugarGrams,
        d.ProteinGrams,
        d.SaltGrams,
        d.Id,
        d.Kind);
}
