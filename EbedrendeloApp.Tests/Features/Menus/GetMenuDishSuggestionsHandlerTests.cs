using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Menus.GetMenuDishSuggestions;
using EbedrendeloApp.Tests.TestSupport;

namespace EbedrendeloApp.Tests.Features.Menus;

public class GetMenuDishSuggestionsHandlerTests : IDisposable
{
    private readonly SqliteDbContextFactory dbFactory = new();
    private readonly GetMenuDishSuggestionsHandler sut;

    public GetMenuDishSuggestionsHandlerTests()
    {
        sut = new GetMenuDishSuggestionsHandler(dbFactory);
    }

    public void Dispose() => dbFactory.Dispose();

    [Fact]
    public async Task Returns_empty_lists_when_the_catalog_is_empty()
    {
        var result = await sut.Handle(new GetMenuDishSuggestionsQuery(), CancellationToken.None);

        Assert.Empty(result.Soups);
        Assert.Empty(result.MainCourses);
    }

    [Fact]
    public async Task Splits_dishes_by_kind_with_their_allergens()
    {
        await using (var db = dbFactory.CreateDbContext())
        {
            db.MenuDishes.AddRange(
                new MenuDish { Kind = MenuDishKind.Leves, Name = "Gulyásleves", Allergens = "zeller" },
                new MenuDish { Kind = MenuDishKind.Leves, Name = "Húsleves", Allergens = null },
                new MenuDish { Kind = MenuDishKind.Foetel, Name = "Rántott hús", Allergens = "glutén, tojás" });
            await db.SaveChangesAsync();
        }

        var result = await sut.Handle(new GetMenuDishSuggestionsQuery(), CancellationToken.None);

        Assert.Equal(2, result.Soups.Count);
        Assert.Contains(result.Soups, d => d.Name == "Gulyásleves" && d.Allergens == "zeller");
        Assert.Contains(result.Soups, d => d.Name == "Húsleves" && d.Allergens == null);

        var mainCourse = Assert.Single(result.MainCourses);
        Assert.Equal("Rántott hús", mainCourse.Name);
        Assert.Equal("glutén, tojás", mainCourse.Allergens);
    }

    [Fact]
    public async Task Includes_the_nutrition_fields()
    {
        await using (var db = dbFactory.CreateDbContext())
        {
            db.MenuDishes.Add(new MenuDish
            {
                Kind = MenuDishKind.Leves,
                Name = "Mentás zöldborsóleves",
                Allergens = "1,8,11",
                EnergyKcal = 108,
                FatGrams = 1.8m,
                SaturatedFatGrams = 0.4m,
                CarbohydrateGrams = 16.0m,
                SugarGrams = 2.1m,
                ProteinGrams = 6.0m,
                SaltGrams = 0.14m,
            });
            await db.SaveChangesAsync();
        }

        var result = await sut.Handle(new GetMenuDishSuggestionsQuery(), CancellationToken.None);

        var soup = Assert.Single(result.Soups);
        Assert.Equal(108, soup.EnergyKcal);
        Assert.Equal(1.8m, soup.FatGrams);
        Assert.Equal(0.4m, soup.SaturatedFatGrams);
        Assert.Equal(16.0m, soup.CarbohydrateGrams);
        Assert.Equal(2.1m, soup.SugarGrams);
        Assert.Equal(6.0m, soup.ProteinGrams);
        Assert.Equal(0.14m, soup.SaltGrams);
    }
}
