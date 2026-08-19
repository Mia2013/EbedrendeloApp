using Bunit;
using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Components.Pages.Calendar;
using EbedrendeloApp.Features.Calendar.GetExcludedDays;
using EbedrendeloApp.Features.Calendar.GetUncoveredWorkdays;
using EbedrendeloApp.Tests.TestSupport;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace EbedrendeloApp.Tests.Components.Calendar;

public class NonOrderableDaysTests : EbedrendeloApp.Tests.TestSupport.MudBunitContext
{
    public NonOrderableDaysTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
    }

    [Fact]
    public void Combines_excluded_and_uncovered_days_into_one_table()
    {
        var mediator = new FakeMediator();
        mediator.Register<GetExcludedDaysQuery, IReadOnlyList<ExcludedDayDto>>(_ =>
        [
            new ExcludedDayDto(new DateOnly(2026, 8, 19), "Karbantartás", "Rendszer Adminisztrátor", DateTime.UtcNow),
        ]);
        mediator.Register<GetUncoveredWorkdaysQuery, IReadOnlyList<DateOnly>>(_ => [new DateOnly(2026, 10, 6)]);
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<NonOrderableDays>((Bunit.ComponentParameterCollectionBuilder<NonOrderableDays> _) => { });

        Assert.Contains("Kizárva", cut.Markup);
        Assert.Contains("Karbantartás", cut.Markup);
        Assert.Contains("Nincs időszak", cut.Markup);
        Assert.Contains("Visszavonás", cut.Markup);
        // The uncovered-day row is purely informative — no action button next to it.
        Assert.DoesNotContain("Időszak létrehozása", cut.Markup);
    }

    [Fact]
    public void Filter_chip_narrows_the_table_to_excluded_days_only()
    {
        var mediator = new FakeMediator();
        mediator.Register<GetExcludedDaysQuery, IReadOnlyList<ExcludedDayDto>>(_ =>
        [
            new ExcludedDayDto(new DateOnly(2026, 8, 19), "Karbantartás", "Rendszer Adminisztrátor", DateTime.UtcNow),
        ]);
        mediator.Register<GetUncoveredWorkdaysQuery, IReadOnlyList<DateOnly>>(_ => [new DateOnly(2026, 10, 6)]);
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<NonOrderableDays>((Bunit.ComponentParameterCollectionBuilder<NonOrderableDays> _) => { });
        Assert.Contains("2026.10.06.", cut.Markup);

        var excludedChip = cut.FindAll(".mud-chip").First(c => c.TextContent.Contains("Kizárva ·"));
        excludedChip.Click();

        Assert.DoesNotContain("2026.10.06.", cut.Markup);
        Assert.Contains("2026.08.19.", cut.Markup);
    }
}
