using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Menus.CreateMenuDish;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Tests.Features.Menus;

public class CreateMenuDishHandlerTests : IDisposable
{
    private readonly SqliteDbContextFactory dbFactory = new();
    private readonly CreateMenuDishHandler sut;

    public CreateMenuDishHandlerTests()
    {
        sut = new CreateMenuDishHandler(dbFactory);
    }

    public void Dispose() => dbFactory.Dispose();

    [Fact]
    public async Task Creates_a_dish_with_allergens_and_nutrition()
    {
        var result = await sut.Handle(
            new CreateMenuDishCommand(
                MenuDishKind.Leves, "Mentás zöldborsóleves", [1, 8, 11],
                EnergyKcal: 108, FatGrams: 1.8m, SaturatedFatGrams: 0.4m, CarbohydrateGrams: 16.0m,
                SugarGrams: 2.1m, ProteinGrams: 6.0m, SaltGrams: 0.14m),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Mentás zöldborsóleves", result.Value!.Name);
        Assert.Equal("1,8,11", result.Value.Allergens);
        Assert.Equal(108, result.Value.EnergyKcal);
        Assert.Equal(0.14m, result.Value.SaltGrams);

        await using var db = dbFactory.CreateDbContext();
        var dish = await db.MenuDishes.SingleAsync(d => d.Kind == MenuDishKind.Leves && d.Name == "Mentás zöldborsóleves");
        Assert.Equal("1,8,11", dish.Allergens);
        Assert.Equal(6.0m, dish.ProteinGrams);
    }

    [Fact]
    public async Task Rejects_a_duplicate_name_within_the_same_kind()
    {
        await using (var db = dbFactory.CreateDbContext())
        {
            db.MenuDishes.Add(new MenuDish { Kind = MenuDishKind.Leves, Name = "Gulyásleves" });
            await db.SaveChangesAsync();
        }

        var result = await sut.Handle(new CreateMenuDishCommand(MenuDishKind.Leves, "Gulyásleves", []), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.DuplicateName, result.ErrorCode);
    }

    [Fact]
    public async Task Allows_the_same_name_across_different_kinds()
    {
        await using (var db = dbFactory.CreateDbContext())
        {
            db.MenuDishes.Add(new MenuDish { Kind = MenuDishKind.Leves, Name = "Gombakrémleves" });
            await db.SaveChangesAsync();
        }

        var result = await sut.Handle(new CreateMenuDishCommand(MenuDishKind.Foetel, "Gombakrémleves", []), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Two_concurrent_creates_of_the_same_name_never_both_succeed()
    {
        // Same reasoning as UpsertALaCarteItemHandlerTests' equivalent race test: the pre-check can't
        // see another admin's not-yet-committed insert — the (Kind, Name) unique index + the
        // SaveChangesAsync catch block are what actually stop the duplicate.
        var dbPath = Path.Combine(Path.GetTempPath(), $"ebedrendelo-menudish-race-{Guid.NewGuid():N}.db");
        using var factoryA = new FileSqliteDbContextFactory(dbPath, ensureCreated: true);
        using var factoryB = new FileSqliteDbContextFactory(dbPath, ensureCreated: false);

        var handlerA = new CreateMenuDishHandler(factoryA);
        var handlerB = new CreateMenuDishHandler(factoryB);

        var command = new CreateMenuDishCommand(MenuDishKind.Leves, "Gulyásleves", []);
        var results = await Task.WhenAll(
            handlerA.Handle(command, CancellationToken.None),
            handlerB.Handle(command, CancellationToken.None));

        Assert.Equal(1, results.Count(r => r.IsSuccess));
        Assert.Equal(1, results.Count(r => !r.IsSuccess && r.ErrorCode == ErrorCodes.DuplicateName));

        await using var verifyDb = factoryA.CreateDbContext();
        Assert.Single(verifyDb.MenuDishes);
    }
}
