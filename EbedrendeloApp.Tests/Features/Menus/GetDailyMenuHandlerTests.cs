using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Menus.GetDailyMenu;
using EbedrendeloApp.Tests.TestSupport;

namespace EbedrendeloApp.Tests.Features.Menus;

public class GetDailyMenuHandlerTests : IDisposable
{
    private readonly SqliteDbContextFactory dbFactory = new();
    private readonly GetDailyMenuHandler sut;

    public GetDailyMenuHandlerTests()
    {
        sut = new GetDailyMenuHandler(dbFactory);
    }

    public void Dispose() => dbFactory.Dispose();

    [Fact]
    public async Task Returns_null_when_there_is_no_menu()
    {
        var result = await sut.Handle(new GetDailyMenuQuery(new DateOnly(2026, 8, 20), IncludeUnpublished: true), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Hides_unpublished_menu_from_non_admin_caller()
    {
        var date = new DateOnly(2026, 8, 20);
        await SeedMenuAsync(date, isPublished: false);

        var result = await sut.Handle(new GetDailyMenuQuery(date, IncludeUnpublished: false), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Shows_unpublished_menu_to_admin_caller()
    {
        var date = new DateOnly(2026, 8, 20);
        await SeedMenuAsync(date, isPublished: false);

        var result = await sut.Handle(new GetDailyMenuQuery(date, IncludeUnpublished: true), CancellationToken.None);

        Assert.NotNull(result);
        Assert.False(result!.IsPublished);
    }

    [Fact]
    public async Task Excludes_soft_deleted_variants_and_orders_by_sort_order_then_code()
    {
        var date = new DateOnly(2026, 8, 20);
        await using (var db = dbFactory.CreateDbContext())
        {
            var dishB = new MenuDish { Kind = MenuDishKind.Leves, Name = "B menü" };
            var dishA = new MenuDish { Kind = MenuDishKind.Leves, Name = "A menü" };
            var dishRemoved = new MenuDish { Kind = MenuDishKind.Leves, Name = "Törölt" };
            db.MenuDishes.AddRange(dishB, dishA, dishRemoved);
            await db.SaveChangesAsync();

            var menu = new DailyMenu { Date = date, IsPublished = true };
            menu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "B", SoupName = "B menü", SoupDishId = dishB.Id, SortOrder = 1 });
            menu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "A", SoupName = "A menü", SoupDishId = dishA.Id, SortOrder = 0 });
            menu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "C", SoupName = "Törölt", SoupDishId = dishRemoved.Id, SortOrder = 2, RemovedAtUtc = DateTime.UtcNow });
            db.DailyMenus.Add(menu);
            await db.SaveChangesAsync();
        }

        var result = await sut.Handle(new GetDailyMenuQuery(date, IncludeUnpublished: true), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(["A", "B"], result!.Variants.Select(v => v.Code));
    }

    [Fact]
    public async Task Joins_allergens_from_the_dish_catalog_by_id()
    {
        var date = new DateOnly(2026, 8, 20);
        await using (var db = dbFactory.CreateDbContext())
        {
            var soupDish = new MenuDish { Kind = MenuDishKind.Leves, Name = "Gulyásleves", Allergens = "zeller" };
            var mainCourseDish = new MenuDish { Kind = MenuDishKind.Foetel, Name = "Rántott hús", Allergens = "glutén, tojás" };
            db.MenuDishes.AddRange(soupDish, mainCourseDish);
            await db.SaveChangesAsync();

            var menu = new DailyMenu { Date = date, IsPublished = true };
            menu.Variants.Add(new MenuVariant
            {
                DailyMenuId = 0,
                Code = "A",
                SoupName = "Gulyásleves",
                SoupDishId = soupDish.Id,
                MainCourseName = "Rántott hús",
                MainCourseDishId = mainCourseDish.Id,
                SortOrder = 0,
            });
            db.DailyMenus.Add(menu);
            await db.SaveChangesAsync();
        }

        var result = await sut.Handle(new GetDailyMenuQuery(date, IncludeUnpublished: true), CancellationToken.None);

        var variant = Assert.Single(result!.Variants);
        Assert.Equal("zeller", variant.SoupAllergens);
        Assert.Equal("glutén, tojás", variant.MainCourseAllergens);
    }

    [Fact]
    public async Task Joins_allergens_by_id_even_after_the_dish_is_renamed_in_the_catalog()
    {
        // Regression coverage for the FK-based join (MenuVariant.SoupDishId -> MenuDish.Id): renaming a
        // catalog dish must not break the allergen join for menus that already reference it — the old
        // by-name matching this replaced was exactly this fragile.
        var date = new DateOnly(2026, 8, 20);
        await using (var db = dbFactory.CreateDbContext())
        {
            var dish = new MenuDish { Kind = MenuDishKind.Leves, Name = "gulyásleves", Allergens = "zeller" };
            db.MenuDishes.Add(dish);
            await db.SaveChangesAsync();

            var menu = new DailyMenu { Date = date, IsPublished = true };
            menu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "A", SoupName = "gulyásleves", SoupDishId = dish.Id, SortOrder = 0 });
            db.DailyMenus.Add(menu);
            await db.SaveChangesAsync();

            dish.Name = "Gulyásleves";
            await db.SaveChangesAsync();
        }

        var result = await sut.Handle(new GetDailyMenuQuery(date, IncludeUnpublished: true), CancellationToken.None);

        var variant = Assert.Single(result!.Variants);
        Assert.Equal("zeller", variant.SoupAllergens);
    }

    [Fact]
    public async Task Joins_nutrition_from_the_dish_catalog_by_id()
    {
        var date = new DateOnly(2026, 8, 20);
        await using (var db = dbFactory.CreateDbContext())
        {
            var dish = new MenuDish { Kind = MenuDishKind.Leves, Name = "Gulyásleves", EnergyKcal = 108, SaltGrams = 0.14m };
            db.MenuDishes.Add(dish);
            await db.SaveChangesAsync();

            var menu = new DailyMenu { Date = date, IsPublished = true };
            menu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "A", SoupName = "Gulyásleves", SoupDishId = dish.Id, SortOrder = 0 });
            db.DailyMenus.Add(menu);
            await db.SaveChangesAsync();
        }

        var result = await sut.Handle(new GetDailyMenuQuery(date, IncludeUnpublished: true), CancellationToken.None);

        var variant = Assert.Single(result!.Variants);
        Assert.Equal(108, variant.SoupEnergyKcal);
        Assert.Equal(0.14m, variant.SoupSaltGrams);
    }

    private async Task SeedMenuAsync(DateOnly date, bool isPublished)
    {
        await using var db = dbFactory.CreateDbContext();
        var dish = new MenuDish { Kind = MenuDishKind.Leves, Name = "Menü" };
        db.MenuDishes.Add(dish);
        await db.SaveChangesAsync();

        var menu = new DailyMenu { Date = date, IsPublished = isPublished };
        menu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "A", SoupName = "Menü", SoupDishId = dish.Id, SortOrder = 0 });
        db.DailyMenus.Add(menu);
        await db.SaveChangesAsync();
    }
}
