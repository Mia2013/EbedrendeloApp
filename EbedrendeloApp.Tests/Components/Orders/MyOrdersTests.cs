using Bunit;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Components.Pages.Orders;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Calendar;
using EbedrendeloApp.Features.Calendar.GetOrderingPeriods;
using EbedrendeloApp.Features.Orders.GetMyPeriodOrder;
using EbedrendeloApp.Tests.TestSupport;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace EbedrendeloApp.Tests.Components.Orders;

public class MyOrdersTests : MudBunitContext
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Today);
    private static readonly DateOnly Monday = Today.AddDays(-WeekdayIndex(Today));

    private static int WeekdayIndex(DateOnly date) => ((int)date.DayOfWeek + 6) % 7;
    private static readonly OrderingPeriodDto Period = new(1, "Teszt időszak", Monday, Monday.AddDays(4), DateTime.Today.AddDays(-40), true, false);

    private readonly FakeMediator mediator = new();

    public MyOrdersTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Teszt Dolgozó", isAdmin: false));

        mediator.Register<GetOrderingPeriodsQuery, IReadOnlyList<OrderingPeriodDto>>(_ => [Period]);
        Services.AddSingleton<IMediator>(mediator);
    }

    [Fact]
    public void A_day_without_any_order_shows_the_empty_hint()
    {
        mediator.Register<GetMyPeriodOrderQuery, Result<IReadOnlyList<MyPeriodOrderDto>>>(_ => Result.Success<IReadOnlyList<MyPeriodOrderDto>>([]));

        var cut = Render<MyOrders>();

        Assert.Contains("Nem rendeltél erre a napra", cut.Markup);
    }

    [Fact]
    public void An_active_order_placed_by_someone_else_shows_the_placer_name()
    {
        mediator.Register<GetMyPeriodOrderQuery, Result<IReadOnlyList<MyPeriodOrderDto>>>(_ => Result.Success<IReadOnlyList<MyPeriodOrderDto>>(
        [
            new MyPeriodOrderDto(Monday, OrderStatus.Active, "A", "Gulyásleves", 2, "Nagy Anna", DateTime.UtcNow, null, null, null),
        ]));

        var cut = Render<MyOrders>();

        Assert.Contains("A menü — Gulyásleves", cut.Markup);
        Assert.Contains("Leadta: Nagy Anna", cut.Markup);
    }

    [Fact]
    public void A_cancelled_order_shows_its_reason()
    {
        mediator.Register<GetMyPeriodOrderQuery, Result<IReadOnlyList<MyPeriodOrderDto>>>(_ => Result.Success<IReadOnlyList<MyPeriodOrderDto>>(
        [
            new MyPeriodOrderDto(Monday, OrderStatus.Cancelled, "B", "Húsleves", 1, null, DateTime.UtcNow.AddDays(-1),
                CancellationReason.DayExcluded, DateTime.UtcNow, null),
        ]));

        var cut = Render<MyOrders>();

        Assert.Contains("Nap kizárása miatt", cut.Markup);
    }

    [Fact]
    public void A_reassigned_order_shows_the_original_variant_code()
    {
        mediator.Register<GetMyPeriodOrderQuery, Result<IReadOnlyList<MyPeriodOrderDto>>>(_ => Result.Success<IReadOnlyList<MyPeriodOrderDto>>(
        [
            new MyPeriodOrderDto(Monday, OrderStatus.Active, "A", "Csirkepaprikás", 1, null, DateTime.UtcNow, null, null, "B"),
        ]));

        var cut = Render<MyOrders>();

        Assert.Contains("Áthelyezve — eredetileg B menüről", cut.Markup);
    }
}
