using EbedrendeloApp.Common.Allergens;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Data;
using EbedrendeloApp.Features.Menus.GetMenuDishSuggestions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.Menus.UpdateMenuDish;

public sealed class UpdateMenuDishHandler(IDbContextFactory<EbedrendeloDbContext> dbFactory)
    : IRequestHandler<UpdateMenuDishCommand, Result<MenuDishDto>>
{
    public async Task<Result<MenuDishDto>> Handle(UpdateMenuDishCommand request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var dish = await db.MenuDishes.FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken);
        if (dish is null)
        {
            return Result.Failure<MenuDishDto>(ErrorCodes.NotFound, "Az étel nem található.");
        }

        var name = request.Name.Trim();
        if (!string.Equals(dish.Name, name, StringComparison.Ordinal))
        {
            var nameTaken = await db.MenuDishes.AnyAsync(d => d.Id != request.Id && d.Kind == dish.Kind && d.Name == name, cancellationToken);
            if (nameTaken)
            {
                return Result.Failure<MenuDishDto>(ErrorCodes.DuplicateName, "Már létezik ilyen nevű étel.");
            }
        }

        // Teljes felülírás, nem a "üres mező = nincs változás" szabály (mint az UpsertDailyMenuHandlernél)
        // — ez egy explicit szerkesztő űrlap, ami mindig a jelenlegi állapotot mutatja, tehát egy
        // kiürített mező szándékos törlést jelent, nem figyelmen kívül hagyandó üres bevitelt.
        dish.Name = name;
        dish.Allergens = AllergenCatalog.Serialize(request.AllergenIds);
        dish.EnergyKcal = request.EnergyKcal;
        dish.FatGrams = request.FatGrams;
        dish.SaturatedFatGrams = request.SaturatedFatGrams;
        dish.CarbohydrateGrams = request.CarbohydrateGrams;
        dish.SugarGrams = request.SugarGrams;
        dish.ProteinGrams = request.ProteinGrams;
        dish.SaltGrams = request.SaltGrams;

        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(new MenuDishDto(
            dish.Name,
            dish.Allergens,
            dish.EnergyKcal,
            dish.FatGrams,
            dish.SaturatedFatGrams,
            dish.CarbohydrateGrams,
            dish.SugarGrams,
            dish.ProteinGrams,
            dish.SaltGrams,
            dish.Id,
            dish.Kind));
    }
}
