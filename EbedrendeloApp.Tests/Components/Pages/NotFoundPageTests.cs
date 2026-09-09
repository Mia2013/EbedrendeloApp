using Bunit;
using EbedrendeloApp.Tests.TestSupport;
using MudBlazor.Services;

namespace EbedrendeloApp.Tests.Components.Pages;

public class NotFoundPageTests : MudBunitContext
{
    public NotFoundPageTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Speaks_hungarian_and_offers_a_way_back()
    {
        var cut = Render<EbedrendeloApp.Components.Pages.NotFound>();

        Assert.Contains("Nincs ilyen oldal", cut.Markup);
        Assert.Contains("Vissza a kezdőlapra", cut.Markup);
        Assert.DoesNotContain("Sorry", cut.Markup);
    }
}
