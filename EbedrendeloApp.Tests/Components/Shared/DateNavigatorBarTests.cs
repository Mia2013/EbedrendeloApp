using Bunit;
using EbedrendeloApp.Components.Shared;
using EbedrendeloApp.Tests.TestSupport;
using MudBlazor.Services;

namespace EbedrendeloApp.Tests.Components.Shared;

public class DateNavigatorBarTests : MudBunitContext
{
    public DateNavigatorBarTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Renders_the_label()
    {
        var cut = Render<DateNavigatorBar>(p => p.Add(x => x.Label, "2026. 09. 01."));

        Assert.Contains("2026. 09. 01.", cut.Markup);
    }

    [Fact]
    public void Clicking_the_chevrons_reports_the_step_direction()
    {
        var steps = new List<int>();
        var cut = Render<DateNavigatorBar>(p => p
            .Add(x => x.Label, "2026. 09. 01.")
            .Add(x => x.OnStep, (int step) => steps.Add(step)));

        cut.Find("button[title='Előző']").Click();
        cut.Find("button[title='Következő']").Click();

        Assert.Equal([-1, 1], steps);
    }
}
