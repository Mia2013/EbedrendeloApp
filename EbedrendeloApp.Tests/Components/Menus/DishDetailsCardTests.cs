using Bunit;
using EbedrendeloApp.Components.Pages.Menus;
using EbedrendeloApp.Features.Menus.GetMenuDishSuggestions;
using EbedrendeloApp.Tests.TestSupport;
using MudBlazor.Services;

namespace EbedrendeloApp.Tests.Components.Menus;

public class DishDetailsCardTests : MudBunitContext
{
    public DishDetailsCardTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Shows_allergen_names_and_nutrition_values()
    {
        var dish = new MenuDishDto("Gulyásleves", "7,9", EnergyKcal: 108m, FatGrams: 1.8m);

        var cut = Render<DishDetailsCard>(p => p.Add(x => x.Dish, dish));

        Assert.Contains("Tej (laktóz)", cut.Markup);
        Assert.Contains("Zeller", cut.Markup);
        Assert.Contains("Energia (kcal)", cut.Markup);
        Assert.Contains("108", cut.Markup);
        Assert.Contains("Zsír (g)", cut.Markup);
        Assert.Contains(1.8m.ToString("0.##"), cut.Markup);
    }

    [Fact]
    public void With_no_allergens_shows_the_no_allergen_hint()
    {
        var dish = new MenuDishDto("Sima rizs", null);

        var cut = Render<DishDetailsCard>(p => p.Add(x => x.Dish, dish));

        Assert.Contains("Nincs jelölt allergén", cut.Markup);
    }

    [Fact]
    public void Nutrition_fields_that_are_null_are_not_rendered()
    {
        var dish = new MenuDishDto("Sima rizs", null, EnergyKcal: 130m);

        var cut = Render<DishDetailsCard>(p => p.Add(x => x.Dish, dish));

        Assert.Contains("Energia (kcal)", cut.Markup);
        Assert.DoesNotContain("Zsír (g)", cut.Markup);
        Assert.DoesNotContain("Cukor (g)", cut.Markup);
    }
}
