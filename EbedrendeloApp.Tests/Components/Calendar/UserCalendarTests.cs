using Bunit;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Components.Pages.Calendar;
using EbedrendeloApp.Features.Calendar;
using EbedrendeloApp.Features.Calendar.GetOrderableDays;
using EbedrendeloApp.Features.Calendar.GetOrderingPeriods;
using EbedrendeloApp.Features.Menus;
using EbedrendeloApp.Features.Menus.GetPeriodMenu;
using EbedrendeloApp.Features.Orders;
using EbedrendeloApp.Features.Orders.CancelMenuOrders;
using EbedrendeloApp.Features.Orders.PlacePeriodOrder;
using EbedrendeloApp.Tests.TestSupport;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace EbedrendeloApp.Tests.Components.Calendar;

public class UserCalendarTests : MudBunitContext
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Today);
    private static readonly OrderingPeriodDto Period = new(1, "Teszt időszak", Today.AddDays(-30), Today.AddDays(30), DateTime.Today.AddDays(-40), true, false);

    private readonly FakeMediator mediator = new();
    private readonly FakeCurrentUser currentUser = new(1, "Teszt Dolgozó", isAdmin: false, colleagues: [new DevUserOption(2, "Kovács Anna", "User")]);

    public UserCalendarTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<ICurrentUser>(currentUser);
        Services.AddSingleton<IDevUserSwitcher>(currentUser);

        mediator.Register<GetOrderingPeriodsQuery, IReadOnlyList<OrderingPeriodDto>>(_ => [Period]);
        mediator.Register<GetPeriodMenuQuery, Result<IReadOnlyList<DailyMenuDto>>>(_ => Result.Success<IReadOnlyList<DailyMenuDto>>([]));
        Services.AddSingleton<IMediator>(mediator);
    }

    [Fact]
    public void Shows_the_reason_text_for_a_day_that_cannot_be_ordered()
    {
        mediator.Register<GetOrderableDaysQuery, Result<IReadOnlyList<OrderableDayDto>>>(_ => Result.Success<IReadOnlyList<OrderableDayDto>>(
        [
            new OrderableDayDto(Today, false, false, null, null, ErrorCodes.MenuNotPublished, null),
        ]));

        var cut = Render<UserCalendar>();

        Assert.Contains("Erre a napra még nincs publikált menü", cut.Markup);
    }

    [Fact]
    public void Shows_a_checkbox_per_variant_for_an_orderable_day_with_no_active_order()
    {
        mediator.Register<GetOrderableDaysQuery, Result<IReadOnlyList<OrderableDayDto>>>(_ => Result.Success<IReadOnlyList<OrderableDayDto>>(
        [
            new OrderableDayDto(Today, true, false, null, null, ErrorCodes.NoActiveOrder, null),
        ]));
        mediator.Register<GetPeriodMenuQuery, Result<IReadOnlyList<DailyMenuDto>>>(_ => Result.Success<IReadOnlyList<DailyMenuDto>>(
        [
            new DailyMenuDto(Today, true, null,
            [
                new MenuVariantDto("A", "Gulyásleves", "Csirkepaprikás", 0),
                new MenuVariantDto("B", "Húsleves", null, 1),
            ]),
        ]));

        var cut = Render<UserCalendar>();

        Assert.Contains("A menü — Gulyásleves", cut.Markup);
        Assert.Contains("B menü — Húsleves", cut.Markup);
        Assert.Equal(2, cut.FindAll("input[type=checkbox]").Count(c => !c.HasAttribute("disabled")));
    }

    [Fact]
    public void Checking_a_variant_shows_the_submit_bar_with_the_selected_count()
    {
        mediator.Register<GetOrderableDaysQuery, Result<IReadOnlyList<OrderableDayDto>>>(_ => Result.Success<IReadOnlyList<OrderableDayDto>>(
        [
            new OrderableDayDto(Today, true, false, null, null, ErrorCodes.NoActiveOrder, null),
        ]));
        mediator.Register<GetPeriodMenuQuery, Result<IReadOnlyList<DailyMenuDto>>>(_ => Result.Success<IReadOnlyList<DailyMenuDto>>(
        [
            new DailyMenuDto(Today, true, null, [new MenuVariantDto("A", "Gulyásleves", null, 0)]),
        ]));

        var cut = Render<UserCalendar>();

        Assert.DoesNotContain("kiválasztva a rendeléshez", cut.Markup);

        var checkbox = cut.Find("input[type=checkbox]:not([disabled])");
        checkbox.Change(true);

        Assert.Contains("1 nap</strong> kiválasztva a rendeléshez", cut.Markup);
        Assert.Contains("Rendelés leadása (1 nap)", cut.Markup);
    }

    [Fact]
    public async Task Submitting_sends_the_selected_days_to_PlacePeriodOrderCommand()
    {
        mediator.Register<GetOrderableDaysQuery, Result<IReadOnlyList<OrderableDayDto>>>(_ => Result.Success<IReadOnlyList<OrderableDayDto>>(
        [
            new OrderableDayDto(Today, true, false, null, null, ErrorCodes.NoActiveOrder, null),
        ]));
        mediator.Register<GetPeriodMenuQuery, Result<IReadOnlyList<DailyMenuDto>>>(_ => Result.Success<IReadOnlyList<DailyMenuDto>>(
        [
            new DailyMenuDto(Today, true, null, [new MenuVariantDto("A", "Gulyásleves", null, 0)]),
        ]));

        PlacePeriodOrderCommand? sentCommand = null;
        mediator.Register<PlacePeriodOrderCommand, Result<BatchOrderResult>>(cmd =>
        {
            sentCommand = cmd;
            return Result.Success(new BatchOrderResult([new DayResult(Today, "A")], []));
        });

        Render<MudDialogProvider>();
        var cut = Render<UserCalendar>();

        var checkbox = cut.Find("input[type=checkbox]:not([disabled])");
        checkbox.Change(true);

        var submitButton = cut.FindAll("button").First(b => b.TextContent.Contains("Rendelés leadása"));
        await cut.InvokeAsync(() => submitButton.Click());

        Assert.NotNull(sentCommand);
        Assert.Equal(1, sentCommand!.TargetUserId);
        Assert.Equal(1, sentCommand.PlacedByUserId);
        Assert.Equal(Today, sentCommand.Days.Single().Date);
        Assert.Equal("A", sentCommand.Days.Single().VariantCode);
    }

    [Fact]
    public void Shows_a_target_user_picker_defaulting_to_myself()
    {
        // A MudSelect le nem nyitott állapotban csak a kiválasztott elemet rendereli a DOM-ba — a
        // kollégalista tartalmát a lenti két teszt bizonyítja funkcionálisan (ValueChanged-en keresztül).
        mediator.Register<GetOrderableDaysQuery, Result<IReadOnlyList<OrderableDayDto>>>(_ => Result.Success<IReadOnlyList<OrderableDayDto>>([]));

        var cut = Render<UserCalendar>();

        Assert.Contains("Kinek rendelek", cut.Markup);
        Assert.Contains("Magamnak", cut.Markup);
    }

    [Fact]
    public async Task Picking_a_colleague_sends_their_id_as_TargetUserId_and_keeps_the_real_placer_as_PlacedByUserId()
    {
        mediator.Register<GetOrderableDaysQuery, Result<IReadOnlyList<OrderableDayDto>>>(_ => Result.Success<IReadOnlyList<OrderableDayDto>>(
        [
            new OrderableDayDto(Today, true, false, null, null, ErrorCodes.NoActiveOrder, null),
        ]));
        mediator.Register<GetPeriodMenuQuery, Result<IReadOnlyList<DailyMenuDto>>>(_ => Result.Success<IReadOnlyList<DailyMenuDto>>(
        [
            new DailyMenuDto(Today, true, null, [new MenuVariantDto("A", "Gulyásleves", null, 0)]),
        ]));

        PlacePeriodOrderCommand? sentCommand = null;
        mediator.Register<PlacePeriodOrderCommand, Result<BatchOrderResult>>(cmd =>
        {
            sentCommand = cmd;
            return Result.Success(new BatchOrderResult([new DayResult(Today, "A")], []));
        });

        Render<MudDialogProvider>();
        var cut = Render<UserCalendar>();

        var targetSelect = cut.FindComponents<MudSelect<int?>>().Single(s => s.Instance.Label == "Kinek rendelek");
        await cut.InvokeAsync(() => targetSelect.Instance.ValueChanged.InvokeAsync(2));

        var checkbox = cut.Find("input[type=checkbox]:not([disabled])");
        checkbox.Change(true);

        var submitButton = cut.FindAll("button").First(b => b.TextContent.Contains("Rendelés leadása"));
        await cut.InvokeAsync(() => submitButton.Click());

        Assert.NotNull(sentCommand);
        Assert.Equal(2, sentCommand!.TargetUserId);
        Assert.Equal(1, sentCommand.PlacedByUserId);
    }

    [Fact]
    public async Task Picking_a_colleague_reloads_the_calendar_for_their_id()
    {
        int? requestedUserId = null;
        mediator.Register<GetOrderableDaysQuery, Result<IReadOnlyList<OrderableDayDto>>>(q =>
        {
            requestedUserId = q.UserId;
            return Result.Success<IReadOnlyList<OrderableDayDto>>([]);
        });

        var cut = Render<UserCalendar>();
        Assert.Equal(1, requestedUserId);

        var targetSelect = cut.FindComponents<MudSelect<int?>>().Single(s => s.Instance.Label == "Kinek rendelek");
        await cut.InvokeAsync(() => targetSelect.Instance.ValueChanged.InvokeAsync(2));

        Assert.Equal(2, requestedUserId);
        Assert.Contains("Kovács Anna nevében", cut.Markup);
    }

    [Fact]
    public void An_active_and_cancellable_order_shows_a_locked_checkbox_and_a_cancel_toggle()
    {
        mediator.Register<GetOrderableDaysQuery, Result<IReadOnlyList<OrderableDayDto>>>(_ => Result.Success<IReadOnlyList<OrderableDayDto>>(
        [
            new OrderableDayDto(Today, false, true, "A", "Gulyásleves", ErrorCodes.AlreadyOrdered, null),
        ]));

        var cut = Render<UserCalendar>();

        Assert.Contains("A menü — Gulyásleves", cut.Markup);
        Assert.Contains("Lemondás", cut.Markup);
        var checkboxes = cut.FindAll(".order-calendar__cell input[type=checkbox]");
        Assert.Equal(2, checkboxes.Count);
        Assert.True(checkboxes[0].HasAttribute("disabled")); // the read-only "active order" checkbox
        Assert.False(checkboxes[1].HasAttribute("disabled")); // the togglable "mark for cancellation" checkbox
    }

    [Fact]
    public void An_active_but_not_cancellable_order_shows_only_the_locked_checkbox_and_the_reason()
    {
        mediator.Register<GetOrderableDaysQuery, Result<IReadOnlyList<OrderableDayDto>>>(_ => Result.Success<IReadOnlyList<OrderableDayDto>>(
        [
            new OrderableDayDto(Today, false, false, "A", "Gulyásleves", ErrorCodes.DeadlinePassed, null),
        ]));

        var cut = Render<UserCalendar>();

        Assert.Contains("A módosítási határidő lejárt", cut.Markup);
        Assert.Single(cut.FindAll(".order-calendar__cell input[type=checkbox]"));
    }

    [Fact]
    public void Checking_the_cancel_toggle_shows_the_cancel_submit_bar_with_the_selected_count()
    {
        mediator.Register<GetOrderableDaysQuery, Result<IReadOnlyList<OrderableDayDto>>>(_ => Result.Success<IReadOnlyList<OrderableDayDto>>(
        [
            new OrderableDayDto(Today, false, true, "A", "Gulyásleves", ErrorCodes.AlreadyOrdered, null),
        ]));

        var cut = Render<UserCalendar>();

        Assert.DoesNotContain("kiválasztva lemondásra", cut.Markup);

        var cancelCheckbox = cut.FindAll("input[type=checkbox]").Single(c => !c.HasAttribute("disabled"));
        cancelCheckbox.Change(true);

        Assert.Contains("1 nap</strong> kiválasztva lemondásra", cut.Markup);
        Assert.Contains("Lemondás megerősítése (1 nap)", cut.Markup);

        cancelCheckbox.Change(false);

        Assert.DoesNotContain("kiválasztva lemondásra", cut.Markup);
    }

    [Fact]
    public async Task Submitting_cancellations_sends_all_marked_dates_in_one_CancelMenuOrdersCommand_call()
    {
        var tomorrow = Today.AddDays(1);
        mediator.Register<GetOrderableDaysQuery, Result<IReadOnlyList<OrderableDayDto>>>(_ => Result.Success<IReadOnlyList<OrderableDayDto>>(
        [
            new OrderableDayDto(Today, false, true, "A", "Gulyásleves", ErrorCodes.AlreadyOrdered, null),
            new OrderableDayDto(tomorrow, false, true, "B", "Húsleves", ErrorCodes.AlreadyOrdered, null),
        ]));

        CancelMenuOrdersCommand? sentCommand = null;
        mediator.Register<CancelMenuOrdersCommand, Result<BatchOrderResult>>(cmd =>
        {
            sentCommand = cmd;
            return Result.Success(new BatchOrderResult([new DayResult(Today, "A"), new DayResult(tomorrow, "B")], []));
        });

        Render<MudDialogProvider>();
        var cut = Render<UserCalendar>();

        foreach (var checkbox in cut.FindAll("input[type=checkbox]").Where(c => !c.HasAttribute("disabled")))
        {
            checkbox.Change(true);
        }

        var submitButton = cut.FindAll("button").First(b => b.TextContent.Contains("Lemondás megerősítése"));
        await cut.InvokeAsync(() => submitButton.Click());

        Assert.NotNull(sentCommand);
        Assert.Equal(1, sentCommand!.TargetUserId);
        Assert.Equal(1, sentCommand.CancelledByUserId);
        Assert.Equal([Today, tomorrow], sentCommand.Dates.OrderBy(d => d));
    }

    [Fact]
    public async Task Successful_cancellation_clears_the_selection_and_opens_the_result_dialog()
    {
        mediator.Register<GetOrderableDaysQuery, Result<IReadOnlyList<OrderableDayDto>>>(_ => Result.Success<IReadOnlyList<OrderableDayDto>>(
        [
            new OrderableDayDto(Today, false, true, "A", "Gulyásleves", ErrorCodes.AlreadyOrdered, null),
        ]));
        mediator.Register<CancelMenuOrdersCommand, Result<BatchOrderResult>>(
            _ => Result.Success(new BatchOrderResult([new DayResult(Today, "A")], [])));

        var provider = Render<MudDialogProvider>();
        var cut = Render<UserCalendar>();

        var cancelCheckbox = cut.FindAll("input[type=checkbox]").Single(c => !c.HasAttribute("disabled"));
        cancelCheckbox.Change(true);

        var submitButton = cut.FindAll("button").First(b => b.TextContent.Contains("Lemondás megerősítése"));
        await cut.InvokeAsync(() => submitButton.Click());

        Assert.DoesNotContain("kiválasztva lemondásra", cut.Markup);
        Assert.Contains("Lemondás eredménye", provider.Markup);
    }

    [Fact]
    public async Task A_skipped_cancellation_shows_the_reason_via_the_result_dialog()
    {
        mediator.Register<GetOrderableDaysQuery, Result<IReadOnlyList<OrderableDayDto>>>(_ => Result.Success<IReadOnlyList<OrderableDayDto>>(
        [
            new OrderableDayDto(Today, false, true, "A", "Gulyásleves", ErrorCodes.AlreadyOrdered, null),
        ]));
        mediator.Register<CancelMenuOrdersCommand, Result<BatchOrderResult>>(
            _ => Result.Success(new BatchOrderResult([], [new DaySkip(Today, ErrorCodes.DayClosed)])));

        var provider = Render<MudDialogProvider>();
        var cut = Render<UserCalendar>();

        var cancelCheckbox = cut.FindAll("input[type=checkbox]").Single(c => !c.HasAttribute("disabled"));
        cancelCheckbox.Change(true);

        var submitButton = cut.FindAll("button").First(b => b.TextContent.Contains("Lemondás megerősítése"));
        await cut.InvokeAsync(() => submitButton.Click());

        Assert.Contains("A nap már le van zárva", provider.Markup);
    }

    [Fact]
    public async Task Picking_a_colleague_and_cancelling_sends_their_id_as_TargetUserId_and_keeps_the_real_canceller_as_CancelledByUserId()
    {
        mediator.Register<GetOrderableDaysQuery, Result<IReadOnlyList<OrderableDayDto>>>(_ => Result.Success<IReadOnlyList<OrderableDayDto>>(
        [
            new OrderableDayDto(Today, false, true, "A", "Gulyásleves", ErrorCodes.AlreadyOrdered, null),
        ]));

        CancelMenuOrdersCommand? sentCommand = null;
        mediator.Register<CancelMenuOrdersCommand, Result<BatchOrderResult>>(cmd =>
        {
            sentCommand = cmd;
            return Result.Success(new BatchOrderResult([new DayResult(Today, "A")], []));
        });

        Render<MudDialogProvider>();
        var cut = Render<UserCalendar>();

        var targetSelect = cut.FindComponents<MudSelect<int?>>().Single(s => s.Instance.Label == "Kinek rendelek");
        await cut.InvokeAsync(() => targetSelect.Instance.ValueChanged.InvokeAsync(2));

        var cancelCheckbox = cut.FindAll("input[type=checkbox]").Single(c => !c.HasAttribute("disabled"));
        cancelCheckbox.Change(true);

        var submitButton = cut.FindAll("button").First(b => b.TextContent.Contains("Lemondás megerősítése"));
        await cut.InvokeAsync(() => submitButton.Click());

        Assert.NotNull(sentCommand);
        Assert.Equal(2, sentCommand!.TargetUserId);
        Assert.Equal(1, sentCommand.CancelledByUserId);
    }
}
