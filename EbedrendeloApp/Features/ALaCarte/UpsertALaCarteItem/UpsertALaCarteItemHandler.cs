using EbedrendeloApp.Common.Allergens;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Data;
using EbedrendeloApp.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.ALaCarte.UpsertALaCarteItem;

public sealed class UpsertALaCarteItemHandler(IDbContextFactory<EbedrendeloDbContext> dbFactory)
    : IRequestHandler<UpsertALaCarteItemCommand, Result<ALaCarteItemDto>>
{
    public async Task<Result<ALaCarteItemDto>> Handle(UpsertALaCarteItemCommand request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var name = request.Name.Trim();

        ALaCarteItem item;
        if (request.Id is { } id)
        {
            var existing = await db.ALaCarteItems.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
            if (existing is null)
            {
                return Result.Failure<ALaCarteItemDto>(ErrorCodes.NotFound, "A tétel nem található.");
            }

            item = existing;
        }
        else
        {
            item = new ALaCarteItem { Name = name, Category = request.Category, PriceHuf = request.PriceHuf };
            db.ALaCarteItems.Add(item);
        }

        // Teljes felülírás, mint az UpdateMenuDishHandler-nél — a szerkesztő dialógus mindig a jelenlegi
        // állapotot mutatja, tehát egy kiürített mező szándékos törlést jelent.
        item.Name = name;
        item.Category = request.Category;
        item.PriceHuf = request.PriceHuf;
        item.IsActive = request.IsActive;
        item.Allergens = AllergenCatalog.Serialize(request.AllergenIds);
        item.EnergyKcal = request.EnergyKcal;
        item.FatGrams = request.FatGrams;
        item.SaturatedFatGrams = request.SaturatedFatGrams;
        item.CarbohydrateGrams = request.CarbohydrateGrams;
        item.SugarGrams = request.SugarGrams;
        item.ProteinGrams = request.ProteinGrams;
        item.SaltGrams = request.SaltGrams;

        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(new ALaCarteItemDto(
            item.Id, item.Name, item.Category, item.PriceHuf, item.IsActive, item.Allergens,
            item.EnergyKcal, item.FatGrams, item.SaturatedFatGrams, item.CarbohydrateGrams,
            item.SugarGrams, item.ProteinGrams, item.SaltGrams));
    }
}
