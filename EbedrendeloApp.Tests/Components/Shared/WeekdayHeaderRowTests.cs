using Bunit;
using EbedrendeloApp.Components.Shared;
using EbedrendeloApp.Tests.TestSupport;
using MudBlazor.Services;

namespace EbedrendeloApp.Tests.Components.Shared;

public class WeekdayHeaderRowTests : MudBunitContext
{
    public WeekdayHeaderRowTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Renders_five_weekdays_each_in_its_own_day_box()
    {
        var cut = Render<WeekdayHeaderRow>();

        var days = cut.FindAll(".weekday-header-row__day");

        Assert.Equal(5, days.Count);
        Assert.Equal(["Hétfő", "Kedd", "Szerda", "Csütörtök", "Péntek"], days.Select(d => d.TextContent.Trim()));
    }
}
