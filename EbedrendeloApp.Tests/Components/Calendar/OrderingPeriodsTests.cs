using Bunit;
using Bunit.TestDoubles;
using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Components.Pages.Calendar;
using EbedrendeloApp.Features.Calendar;
using EbedrendeloApp.Features.Calendar.GetOrderingPeriods;
using EbedrendeloApp.Tests.TestSupport;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace EbedrendeloApp.Tests.Components.Calendar;

public class OrderingPeriodsTests : EbedrendeloApp.Tests.TestSupport.MudBunitContext
{
    public OrderingPeriodsTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Renders_periods_with_open_and_closed_state()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));

        var mediator = new FakeMediator();
        mediator.Register<GetOrderingPeriodsQuery, IReadOnlyList<OrderingPeriodDto>>(_ =>
        [
            new OrderingPeriodDto(1, "2026. augusztus", new DateOnly(2026, 8, 5), new DateOnly(2026, 9, 5), new DateTime(2026, 7, 26, 10, 0, 0), true, false),
            new OrderingPeriodDto(2, "2025. december", new DateOnly(2025, 12, 1), new DateOnly(2026, 1, 5), new DateTime(2025, 11, 21, 10, 0, 0), false, true),
        ]);
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<OrderingPeriods>((Bunit.ComponentParameterCollectionBuilder<OrderingPeriods> _) => { });

        Assert.Contains("2026. augusztus", cut.Markup);
        Assert.Contains("Nyitva", cut.Markup);
        Assert.Contains("2025. december", cut.Markup);
        Assert.Contains("Zárva", cut.Markup);
    }

    [Fact]
    public void Redirects_non_admin_users_to_the_worker_calendar()
    {
        var currentUser = new FakeCurrentUser(2, "Dolgozó Teszt", isAdmin: false);
        Services.AddSingleton<ICurrentUser>(currentUser);
        Services.AddSingleton<IMediator>(new FakeMediator());

        Render<OrderingPeriods>((Bunit.ComponentParameterCollectionBuilder<OrderingPeriods> _) => { });

        var navigationManager = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/naptar", navigationManager.Uri);
    }
}
