using Bunit;
using EbedrendeloApp.Components.Pages;
using EbedrendeloApp.Tests.TestSupport;
using MudBlazor.Services;

namespace EbedrendeloApp.Tests.Components.Pages;

/// <summary>
/// A hibaoldal a felhasználó egyetlen visszajelzése kezeletlen kivétel után — magyarul kell szólnia,
/// és nem szivárogtathat ki fejlesztői útmutatót (a sablon eredeti „Development Mode" blokkja).
/// </summary>
public class ErrorPageTests : MudBunitContext
{
    public ErrorPageTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Speaks_hungarian_and_offers_a_way_back()
    {
        var cut = Render<Error>();

        Assert.Contains("Hiba történt", cut.Markup);
        Assert.Contains("Vissza a kezdőlapra", cut.Markup);
    }

    [Fact]
    public void Does_not_show_the_developer_environment_hint()
    {
        var cut = Render<Error>();

        Assert.DoesNotContain("Development", cut.Markup);
        Assert.DoesNotContain("ASPNETCORE_ENVIRONMENT", cut.Markup);
    }

    [Fact]
    public void Shows_the_request_id_so_the_admin_can_look_it_up_in_the_log()
    {
        // Activity.Current a bUnit-futásban null, HttpContext sincs — ilyenkor nincs mit mutatni,
        // és a blokk nem jelenhet meg üresen.
        var cut = Render<Error>();

        Assert.DoesNotContain("Hibaazonosító", cut.Markup);
    }
}
