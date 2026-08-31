using Bunit;
using Bunit.TestDoubles;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Components.Pages.Orders;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Calendar;
using EbedrendeloApp.Features.Calendar.GetOrderingPeriods;
using EbedrendeloApp.Features.Orders.GetUserOrders;
using EbedrendeloApp.Tests.TestSupport;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace EbedrendeloApp.Tests.Components.Orders;

public class AdminOrdersTests : MudBunitContext
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Today);
    private static readonly DateOnly Monday = Today.AddDays(-WeekdayIndex(Today));
    private static readonly OrderingPeriodDto Period = new(1, "Teszt időszak", Monday, Monday.AddDays(4), DateTime.Today.AddDays(-40), true, false);

    private readonly FakeMediator mediator = new();

    public AdminOrdersTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;

        mediator.Register<GetOrderingPeriodsQuery, IReadOnlyList<OrderingPeriodDto>>(_ => [Period]);
        Services.AddSingleton<IMediator>(mediator);
    }

    private static int WeekdayIndex(DateOnly date) => ((int)date.DayOfWeek + 6) % 7;

    [Fact]
    public void A_non_admin_is_redirected_away()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Teszt Dolgozó", isAdmin: false));
        Services.AddSingleton<IDevUserSwitcher>(new FakeCurrentUser(1, "Teszt Dolgozó", isAdmin: false));
        mediator.Register<GetUserOrdersQuery, Result<IReadOnlyList<UserOrderDto>>>(_ => Result.Success<IReadOnlyList<UserOrderDto>>([]));

        Render<AdminOrders>();

        var navigationManager = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/mai-menu", navigationManager.Uri);
    }

    [Fact]
    public void The_daily_summary_only_counts_active_orders_grouped_by_variant()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        Services.AddSingleton<IDevUserSwitcher>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        mediator.Register<GetUserOrdersQuery, Result<IReadOnlyList<UserOrderDto>>>(_ => Result.Success<IReadOnlyList<UserOrderDto>>(
        [
            new UserOrderDto(1, Monday, 2, "Kovács János", "A", "Gulyásleves", OrderStatus.Active, 2, "Kovács János", DateTime.UtcNow, null, null, null, null),
            new UserOrderDto(2, Monday, 3, "Nagy Anna", "A", "Gulyásleves", OrderStatus.Active, 3, "Nagy Anna", DateTime.UtcNow, null, null, null, null),
            new UserOrderDto(3, Monday, 4, "Szabó Péter", "B", "Húsleves", OrderStatus.Cancelled, 4, "Szabó Péter", DateTime.UtcNow, 5, "Admin", DateTime.UtcNow, CancellationReason.ByUser),
        ]));

        var cut = Render<AdminOrders>();

        Assert.Contains("A menü — Gulyásleves", cut.Markup);
        Assert.Contains(">2<", cut.Markup);
        Assert.DoesNotContain("Húsleves", cut.Markup.Split("Részletes lista")[0]);
    }

    [Fact]
    public void The_status_filter_narrows_the_detailed_table()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        Services.AddSingleton<IDevUserSwitcher>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        mediator.Register<GetUserOrdersQuery, Result<IReadOnlyList<UserOrderDto>>>(_ => Result.Success<IReadOnlyList<UserOrderDto>>(
        [
            new UserOrderDto(1, Monday, 2, "Kovács János", "A", "Gulyásleves", OrderStatus.Active, 2, "Kovács János", DateTime.UtcNow, null, null, null, null),
            new UserOrderDto(2, Monday, 3, "Nagy Anna", "B", "Húsleves", OrderStatus.Cancelled, 3, "Nagy Anna", DateTime.UtcNow, 5, "Admin", DateTime.UtcNow, CancellationReason.ByUser),
        ]));

        var cut = Render<AdminOrders>();

        Assert.Equal(2, cut.FindAll("table tbody tr").Count);

        var statusChip = cut.FindAll(".mud-chip").First(el => el.TextContent.Trim() == "Aktív");
        cut.InvokeAsync(() => statusChip.Click());

        Assert.Single(cut.FindAll("table tbody tr"));
    }
}
