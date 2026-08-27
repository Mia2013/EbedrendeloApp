using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Menus.CreateMenuDish;
using EbedrendeloApp.Features.Menus.UpdateMenuDish;
using EbedrendeloApp.Features.Menus.UpsertDailyMenu;

namespace EbedrendeloApp.Tests.Features.Menus;

/// <summary>
/// Regression tests: the nutrition decimal fields are stored as decimal(6,2) (MenuDishConfiguration —
/// max 9999.99). Without an upper bound in the validators, a value at/above 10000 passed FluentValidation
/// and then threw a raw SQL arithmetic-overflow DbUpdateException at SaveChangesAsync instead of a friendly
/// Result.Failure.
/// </summary>
public class NutritionValidatorBoundsTests
{
    [Fact]
    public void CreateMenuDish_rejects_a_value_that_would_overflow_decimal_6_2()
    {
        var command = new CreateMenuDishCommand(MenuDishKind.Leves, "Leves", [], EnergyKcal: 10000);

        var result = new CreateMenuDishValidator().Validate(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void CreateMenuDish_accepts_the_maximum_representable_value()
    {
        var command = new CreateMenuDishCommand(MenuDishKind.Leves, "Leves", [], EnergyKcal: 9999.99m);

        var result = new CreateMenuDishValidator().Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void UpdateMenuDish_rejects_a_value_that_would_overflow_decimal_6_2()
    {
        var command = new UpdateMenuDishCommand(1, "Leves", [], SaltGrams: 12345);

        var result = new UpdateMenuDishValidator().Validate(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void UpsertDailyMenu_rejects_a_variant_nutrition_value_that_would_overflow_decimal_6_2()
    {
        var command = new UpsertDailyMenuCommand(
            new DateOnly(2026, 8, 20),
            null,
            [new MenuVariantInput("A", "Menü", null, 0, SoupEnergyKcal: 99999)],
            PerformedByUserId: 1);

        var result = new UpsertDailyMenuValidator().Validate(command);

        Assert.False(result.IsValid);
    }
}
