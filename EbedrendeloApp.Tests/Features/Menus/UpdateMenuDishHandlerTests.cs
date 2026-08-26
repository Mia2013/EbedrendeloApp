using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Menus.UpdateMenuDish;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Tests.Features.Menus;

public class UpdateMenuDishHandlerTests : IDisposable
{
    private readonly SqliteDbContextFactory dbFactory = new();
    private readonly UpdateMenuDishHandler sut;

    public UpdateMenuDishHandlerTests()
    {
        sut = new UpdateMenuDishHandler(dbFactory);
    }

    public void Dispose() => dbFactory.Dispose();

    private async Task<int> SeedDishAsync(MenuDishKind kind, string name, string? allergens = null)
    {
        await using var db = dbFactory.CreateDbContext();
        var dish = new MenuDish { Kind = kind, Name = name, Allergens = allergens };
        db.MenuDishes.Add(dish);
        await db.SaveChangesAsync();
        return dish.Id;
    }

    [Fact]
    public async Task Updates_name_allergens_and_nutrition()
    {
        var id = await SeedDishAsync(MenuDishKind.Leves, "Gulyásleves", "9");

        var result = await sut.Handle(
            new UpdateMenuDishCommand(id, "Gulyásleves", [1, 9], EnergyKcal: 108, SaltGrams: 0.14m),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(108, result.Value!.EnergyKcal);
        Assert.Equal(0.14m, result.Value.SaltGrams);
        Assert.Equal("1,9", result.Value.Allergens);

        await using var db = dbFactory.CreateDbContext();
        var dish = await db.MenuDishes.SingleAsync(d => d.Id == id);
        Assert.Equal(108, dish.EnergyKcal);
        Assert.Equal("1,9", dish.Allergens);
    }

    [Fact]
    public async Task Clearing_a_nutrition_field_actually_clears_it_unlike_the_upsert_daily_menu_merge_rule()
    {
        var id = await SeedDishAsync(MenuDishKind.Leves, "Gulyásleves");
        await sut.Handle(new UpdateMenuDishCommand(id, "Gulyásleves", [], EnergyKcal: 108), CancellationToken.None);

        var result = await sut.Handle(new UpdateMenuDishCommand(id, "Gulyásleves", []), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.EnergyKcal);
    }

    [Fact]
    public async Task Rejects_renaming_to_a_name_already_used_by_another_dish_of_the_same_kind()
    {
        await SeedDishAsync(MenuDishKind.Leves, "Húsleves");
        var id = await SeedDishAsync(MenuDishKind.Leves, "Gulyásleves");

        var result = await sut.Handle(new UpdateMenuDishCommand(id, "Húsleves", []), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.DuplicateName, result.ErrorCode);
    }

    [Fact]
    public async Task Allows_keeping_the_same_name()
    {
        var id = await SeedDishAsync(MenuDishKind.Leves, "Gulyásleves");

        var result = await sut.Handle(new UpdateMenuDishCommand(id, "Gulyásleves", [], EnergyKcal: 50), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Returns_not_found_for_an_unknown_id()
    {
        var result = await sut.Handle(new UpdateMenuDishCommand(999, "Bármi", []), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NotFound, result.ErrorCode);
    }
}
