using Bunit;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Components.Pages.Calendar;
using EbedrendeloApp.Features.Calendar;
using EbedrendeloApp.Features.Calendar.GetOrderableDays;
using EbedrendeloApp.Features.Calendar.GetOrderingPeriods;
using EbedrendeloApp.Tests.TestSupport;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace EbedrendeloApp.Tests.Components.Calendar;

public class UserCalendarTests : EbedrendeloApp.Tests.TestSupport.MudBunitContext
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Today);
    private static readonly OrderingPeriodDto Period = new(1, "Teszt időszak", Today.AddDays(-30), Today.AddDays(30), DateTime.Today.AddDays(-40), true, false);

    public UserCalendarTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Teszt Dolgozó", isAdmin: false));
    }

    [Fact]
    public void Renders_the_reason_text_for_each_orderable_day()
    {
        var mediator = new FakeMediator();
        mediator.Register<GetOrderingPeriodsQuery, IReadOnlyList<OrderingPeriodDto>>(_ => [Period]);
        mediator.Register<GetOrderableDaysQuery, Result<IReadOnlyList<OrderableDayDto>>>(_ => Result.Success<IReadOnlyList<OrderableDayDto>>(
        [
            new OrderableDayDto(Today, true, false, null, null, ErrorCodes.NoActiveOrder, null),
        ]));
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<UserCalendar>((Bunit.ComponentParameterCollectionBuilder<UserCalendar> _) => { });

        Assert.Contains("Nincs aktív rendelésed erre a napra", cut.Markup);
    }

    [Fact]
    public void Past_days_are_hidden_by_default_and_revealed_by_the_history_button()
    {
        var pastDay = Today.AddDays(-5);
        var mediator = new FakeMediator();
        mediator.Register<GetOrderingPeriodsQuery, IReadOnlyList<OrderingPeriodDto>>(_ => [Period]);
        mediator.Register<GetOrderableDaysQuery, Result<IReadOnlyList<OrderableDayDto>>>(_ => Result.Success<IReadOnlyList<OrderableDayDto>>(
        [
            new OrderableDayDto(pastDay, false, false, null, null, ErrorCodes.DayExcluded, "RégiIndok"),
            new OrderableDayDto(Today, true, false, null, null, ErrorCodes.NoActiveOrder, null),
        ]));
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<UserCalendar>((Bunit.ComponentParameterCollectionBuilder<UserCalendar> _) => { });

        Assert.DoesNotContain("RégiIndok", cut.Markup);

        var historyButton = cut.FindAll("button").First(b => b.TextContent.Contains("Előzmények mutatása"));
        historyButton.Click();

        Assert.Contains("RégiIndok", cut.Markup);
    }
}
