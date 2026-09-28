using Bunit;
using EbedrendeloApp.Components.Shared;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using MudBlazor.Services;

namespace EbedrendeloApp.Tests.Components.Shared;

/// <summary>
/// A cím <c>&lt;h4&gt;</c>-ként való megjelenése nem kozmetika, hanem a komponens feloldódásának
/// bizonyítéka: ha egy oldalról hiányzik a <c>@using EbedrendeloApp.Components.Shared</c>, a Razor nem
/// hibát ad, hanem ismeretlen HTML-elemként rendereli a <c>&lt;PageHeader&gt;</c>-t (RZ10012 warning),
/// és a címsor, az ikon meg a kártya nyomtalanul eltűnik. Pontosan ez történt öt oldalon, zöld tesztek
/// mellett, mert erre a komponensre nem volt egy teszt sem.
/// </summary>
public class PageHeaderTests : MudBunitContext
{
    public PageHeaderTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Renders_the_title_as_a_heading()
    {
        var cut = Render<PageHeader>(parameters => parameters
            .Add(p => p.Icon, Icons.Material.Filled.Home)
            .Add(p => p.Title, "Egyenlegem"));

        Assert.Equal("Egyenlegem", cut.Find("h4").TextContent.Trim());
    }

    [Fact]
    public void Renders_every_optional_slot_when_given()
    {
        var cut = Render<PageHeader>(parameters => parameters
            .Add(p => p.Icon, Icons.Material.Filled.Home)
            .Add(p => p.Title, "Rendelés")
            .Add(p => p.Description, (RenderFragment)(b => b.AddMarkupContent(0, "<span>Leírás</span>")))
            .Add(p => p.Filters, (RenderFragment)(b => b.AddMarkupContent(0, "<span>Szűrő</span>")))
            .Add(p => p.Actions, (RenderFragment)(b => b.AddMarkupContent(0, "<span>Művelet</span>")))
            .Add(p => p.ChildContent, (RenderFragment)(b => b.AddMarkupContent(0, "<span>Extra</span>"))));

        Assert.Contains("Leírás", cut.Markup);
        Assert.Contains("Szűrő", cut.Markup);
        Assert.Contains("Művelet", cut.Markup);
        Assert.Contains("Extra", cut.Markup);
    }

    [Fact]
    public void Omits_the_optional_slots_when_not_given()
    {
        var cut = Render<PageHeader>(parameters => parameters
            .Add(p => p.Icon, Icons.Material.Filled.Home)
            .Add(p => p.Title, "Rendelés"));

        // A leírás a body2 + mud-text-secondary sor; szűrő és művelet nélkül nem születhet üres sáv sem.
        Assert.Empty(cut.FindAll(".mud-text-secondary"));
    }
}
