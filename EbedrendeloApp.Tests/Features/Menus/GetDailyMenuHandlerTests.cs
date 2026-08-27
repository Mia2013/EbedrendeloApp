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
            var menu = new DailyMenu { Date = date, IsPublished = true };
            menu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "B", Name = "B menü", SortOrder = 1 });
            menu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "A", Name = "A menü", SortOrder = 0 });
            menu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "C", Name = "Törölt", SortOrder = 2, RemovedAtUtc = DateTime.UtcNow });
            db.DailyMenus.Add(menu);
            await db.SaveChangesAsync();
        }

        var result = await sut.Handle(new GetDailyMenuQuery(date, IncludeUnpublished: true), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(["A", "B"], result!.Variants.Select(v => v.Code));
    }

    [Fact]
    public async Task Joins_allergens_from_the_dish_catalog_by_name()
    {
        var date = new DateOnly(2026, 8, 20);
        await using (var db = dbFactory.CreateDbContext())
        {
            var menu = new DailyMenu { Date = date, IsPublished = true };
            menu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "A", Name = "Gulyásleves", Description = "Rántott hús", SortOrder = 0 });
            db.DailyMenus.Add(menu);
            db.MenuDishes.Add(new MenuDish { Kind = MenuDishKind.Leves, Name = "Gulyásleves", Allergens = "zeller" });
            db.MenuDishes.Add(new MenuDish { Kind = MenuDishKind.Foetel, Name = "Rántott hús", Allergens = "glutén, tojás" });
            await db.SaveChangesAsync();
        }

        var result = await sut.Handle(new GetDailyMenuQuery(date, IncludeUnpublished: true), CancellationToken.None);

        var variant = Assert.Single(result!.Variants);
        Assert.Equal("zeller", variant.SoupAllergens);
        Assert.Equal("glutén, tojás", variant.MainCourseAllergens);
    }

    [Fact]
    public async Task Joins_allergens_case_insensitively_after_a_dish_only_casing_rename()
    {
        // Regression test: MenuVariant.Name/Description are denormalized free text, not FK-linked to
        // MenuDish (see MenuDish.cs). A case-only rename via UpdateMenuDishHandler ("gulyásleves" ->
        // "Gulyásleves") only guards against a case-SENSITIVE self-conflict, so it succeeds and leaves
        // already-saved MenuVariant rows with the old casing. The join must still find the dish.
        var date = new DateOnly(2026, 8, 20);
        await using (var db = dbFactory.CreateDbContext())
        {
            var menu = new DailyMenu { Date = date, IsPublished = true };
            menu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "A", Name = "gulyásleves", SortOrder = 0 });
            db.DailyMenus.Add(menu);
            db.MenuDishes.Add(new MenuDish { Kind = MenuDishKind.Leves, Name = "Gulyásleves", Allergens = "zeller" });
            await db.SaveChangesAsync();
        }

        var result = await sut.Handle(new GetDailyMenuQuery(date, IncludeUnpublished: true), CancellationToken.None);

        var variant = Assert.Single(result!.Variants);
        Assert.Equal("zeller", variant.SoupAllergens);
    }

    [Fact]
    public async Task Joins_nutrition_from_the_dish_catalog_by_name()
    {
        var date = new DateOnly(2026, 8, 20);
        await using (var db = dbFactory.CreateDbContext())
        {
            var menu = new DailyMenu { Date = date, IsPublished = true };
            menu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "A", Name = "Gulyásleves", SortOrder = 0 });
            db.DailyMenus.Add(menu);
            db.MenuDishes.Add(new MenuDish { Kind = MenuDishKind.Leves, Name = "Gulyásleves", EnergyKcal = 108, SaltGrams = 0.14m });
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
        var menu = new DailyMenu { Date = date, IsPublished = isPublished };
        menu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "A", Name = "Menü", SortOrder = 0 });
        db.DailyMenus.Add(menu);
        await db.SaveChangesAsync();
    }
}
