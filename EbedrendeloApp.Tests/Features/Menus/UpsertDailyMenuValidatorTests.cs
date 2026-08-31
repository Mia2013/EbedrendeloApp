using EbedrendeloApp.Features.Menus.UpsertDailyMenu;

namespace EbedrendeloApp.Tests.Features.Menus;

/// <summary>
/// The variant Code is a closed set (A/B/C) — display text like "A menü" is always the UI's job, the
/// Code itself must never carry more than the bare letter.
/// </summary>
public class UpsertDailyMenuValidatorTests
{
    private static UpsertDailyMenuCommand CommandWithCode(string code) => new(
        new DateOnly(2026, 8, 20), null, [new MenuVariantInput(code, 1, null, 0)], PerformedByUserId: 1);

    [Theory]
    [InlineData("A")]
    [InlineData("B")]
    [InlineData("C")]
    public void Accepts_the_three_allowed_codes(string code)
    {
        var result = new UpsertDailyMenuValidator().Validate(CommandWithCode(code));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("D")]
    [InlineData("a")]
    [InlineData("A menü")]
    [InlineData("")]
    public void Rejects_anything_outside_the_three_allowed_codes(string code)
    {
        var result = new UpsertDailyMenuValidator().Validate(CommandWithCode(code));

        Assert.False(result.IsValid);
    }
}
