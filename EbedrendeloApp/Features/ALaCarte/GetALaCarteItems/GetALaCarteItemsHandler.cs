using EbedrendeloApp.Data;
using EbedrendeloApp.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.ALaCarte.GetALaCarteItems;

public sealed class GetALaCarteItemsHandler(IDbContextFactory<EbedrendeloDbContext> dbFactory)
    : IRequestHandler<GetALaCarteItemsQuery, IReadOnlyList<ALaCarteItemDto>>
{
    public async Task<IReadOnlyList<ALaCarteItemDto>> Handle(GetALaCarteItemsQuery request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Plain OrderBy/ThenBy (no StringComparer) — translates to server-side SQL, unlike a .NET
        // StringComparer, which EF Core can't translate (GetMenuDishSuggestionsHandler's precedent).
        var items = await db.ALaCarteItems
            .OrderBy(i => i.Category).ThenBy(i => i.Name)
            .ToListAsync(cancellationToken);

        return items.Select(ToDto).ToList();
    }

    private static ALaCarteItemDto ToDto(ALaCarteItem i) => new(
        i.Id, i.Name, i.Category, i.PriceHuf, i.IsActive, i.Allergens,
        i.EnergyKcal, i.FatGrams, i.SaturatedFatGrams, i.CarbohydrateGrams, i.SugarGrams, i.ProteinGrams, i.SaltGrams);
}
