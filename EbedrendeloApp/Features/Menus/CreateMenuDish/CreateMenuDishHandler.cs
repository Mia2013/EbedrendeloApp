using EbedrendeloApp.Common.Allergens;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Data;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Features.Menus.GetMenuDishSuggestions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.Menus.CreateMenuDish;

public sealed class CreateMenuDishHandler(IDbContextFactory<EbedrendeloDbContext> dbFactory)
    : IRequestHandler<CreateMenuDishCommand, Result<MenuDishDto>>
{
    public async Task<Result<MenuDishDto>> Handle(CreateMenuDishCommand request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var name = request.Name.Trim();

        // Pre-check instead of letting the unique index throw — same reasoning as the ordering period
        // overlap check (UpsertOrderingPeriodHandler): a friendly Result.Failure beats a raw DB exception.
        var alreadyExists = await db.MenuDishes.AnyAsync(d => d.Kind == request.Kind && d.Name == name, cancellationToken);
        if (alreadyExists)
        {
            return Result.Failure<MenuDishDto>(ErrorCodes.DuplicateName, "Már létezik ilyen nevű étel.");
        }

        var dish = new MenuDish
        {
            Kind = request.Kind,
            Name = name,
            Allergens = AllergenCatalog.Serialize(request.AllergenIds),
            EnergyKcal = request.EnergyKcal,
            FatGrams = request.FatGrams,
            SaturatedFatGrams = request.SaturatedFatGrams,
            CarbohydrateGrams = request.CarbohydrateGrams,
            SugarGrams = request.SugarGrams,
            ProteinGrams = request.ProteinGrams,
            SaltGrams = request.SaltGrams,
        };
        db.MenuDishes.Add(dish);
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
