using Bunit;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Components.Pages.Calendar;
using EbedrendeloApp.Components.Shared;
using EbedrendeloApp.Features.Billing.GetMyBalance;
using EbedrendeloApp.Features.Calendar;
using EbedrendeloApp.Features.Calendar.GetOrderableDays;
using EbedrendeloApp.Features.Calendar.GetOrderingPeriods;
using EbedrendeloApp.Features.Menus;
using EbedrendeloApp.Features.Menus.GetPeriodMenu;
using EbedrendeloApp.Features.Orders;
using EbedrendeloApp.Features.Orders.CancelMenuOrders;
using EbedrendeloApp.Features.Orders.PlacePeriodOrder;
using EbedrendeloApp.Features.Users.GetUsers;
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

    // A fixture felhasználója admin (neki autocomplete jut a kolléga-választóban); a dolgozói,
    // rejtett változatot külön teszt fedi.
    private readonly FakeCurrentUser currentUser = new(1, "Teszt Admin", isAdmin: true);

    public UserCalendarTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<ICurrentUser>(currentUser);
        Services.AddSingleton<IDevUserSwitcher>(currentUser);

        mediator.Register<GetOrderingPeriodsQuery, IReadOnlyList<OrderingPeriodDto>>(_ => [Period]);
        mediator.Register<GetPeriodMenuQuery, Result<IReadOnlyList<DailyMenuDto>>>(_ => Result.Success<IReadOnlyList<DailyMenuDto>>([]));
        mediator.Register<GetMyBalanceQuery, Result<int>>(_ => Result.Success(0));
        mediator.Register<GetUsersQuery, Result<IReadOnlyList<UserOptionDto>>>(_ => Result.Success<IReadOnlyList<UserOptionDto>>(
        [
            new UserOptionDto(1, "admin", 1, "Teszt Admin", "Admin", null, null),
            new UserOptionDto(2, "kanna", 2, "Kovács Anna", "User", null, null),
        ]));
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
    public void Checking_a_variant_shows_the_submit_bar_with_the_selected_count_and_price()
    {
        mediator.Register<GetOrderableDaysQuery, Result<IReadOnlyList<OrderableDayDto>>>(_ => Result.Success<IReadOnlyList<OrderableDayDto>>(
        [
            new OrderableDayDto(Today, true, false, null, null, ErrorCodes.NoActiveOrder, null, MenuPortionHuf: 1400),
        ]));
        mediator.Register<GetPeriodMenuQuery, Result<IReadOnlyList<DailyMenuDto>>>(_ => Result.Success<IReadOnlyList<DailyMenuDto>>(
        [
            new DailyMenuDto(Today, true, null, [new MenuVariantDto("A", "Gulyásleves", null, 0)]),
        ]));

        var cut = Render<UserCalendar>();

        Assert.DoesNotContain("Rendelés leadása", cut.Markup);

        var checkbox = cut.Find("input[type=checkbox]:not([disabled])");
        checkbox.Change(true);

        Assert.Contains("1\u00A0400 Ft", cut.Markup);
        Assert.Contains("Rendelés leadása (1 nap, 1\u00A0400 Ft)", cut.Markup);
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

    private static readonly UserOptionDto Colleague = new(2, "kanna", 2, "Kovács Anna", "User", "Gyártás", "Logisztika");

    /// <summary>A kolléga kiválasztása a <c>ColleaguePicker</c> dolga; itt a `UserCalendar` bekötését
    /// teszteljük, ezért a gyerekkomponens visszahívását váltjuk ki közvetlenül.</summary>
    private static async Task PickColleagueAsync(IRenderedComponent<UserCalendar> cut, UserOptionDto colleague)
    {
        var picker = cut.FindComponent<ColleaguePicker>();
        await cut.InvokeAsync(() => picker.Instance.SelectedChanged.InvokeAsync(colleague));
    }

    [Fact]
    public void Starts_on_my_own_calendar_without_advertising_the_colleague_feature()
    {
        // A dolgozónak a más nevében rendelést NEM kínáljuk fel: felirat nélküli, semleges ikon van
        // csak, ami magától nem hívja fel rá a figyelmet.
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(2, "Teszt Dolgozó", isAdmin: false));
        mediator.Register<GetOrderableDaysQuery, Result<IReadOnlyList<OrderableDayDto>>>(_ => Result.Success<IReadOnlyList<OrderableDayDto>>([]));

        var cut = Render<UserCalendar>();

        Assert.DoesNotContain("Rendelés kolléga nevében", cut.Markup);
        Assert.DoesNotContain("Kinek rendelek", cut.Markup);
        Assert.DoesNotContain("Másik dolgozó naptára", cut.Markup);
        Assert.DoesNotContain("az ő rendelése lesz", cut.Markup);
        Assert.NotNull(cut.FindComponent<ColleaguePicker>());
    }

    [Fact]
    public void Picking_a_colleague_switches_the_calendar_to_theirs_visibly()
    {
        mediator.Register<GetOrderableDaysQuery, Result<IReadOnlyList<OrderableDayDto>>>(_ => Result.Success<IReadOnlyList<OrderableDayDto>>([]));

        var cut = Render<UserCalendar>();
        Assert.DoesNotContain("az ő rendelése lesz", cut.Markup);

        PickColleagueAsync(cut, Colleague).GetAwaiter().GetResult();

        Assert.Contains("Kovács Anna", cut.Markup);
        Assert.Contains("az ő rendelése lesz", cut.Markup);
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

        await PickColleagueAsync(cut, Colleague);

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

        await PickColleagueAsync(cut, Colleague);

        Assert.Equal(2, requestedUserId);
        Assert.Contains("Kovács Anna", cut.Markup);
    }

    [Fact]
    public void An_active_and_cancellable_order_shows_a_locked_checkbox_and_a_cancel_icon()
    {
        mediator.Register<GetOrderableDaysQuery, Result<IReadOnlyList<OrderableDayDto>>>(_ => Result.Success<IReadOnlyList<OrderableDayDto>>(
        [
            new OrderableDayDto(Today, false, true, "A", "Gulyásleves", ErrorCodes.AlreadyOrdered, null),
        ]));

        var cut = Render<UserCalendar>();

        Assert.Contains("A menü — Gulyásleves", cut.Markup);
        var checkboxes = cut.FindAll(".week-grid__cell input[type=checkbox]");
        var checkbox = Assert.Single(checkboxes); // only the read-only "active order" checkbox
        Assert.True(checkbox.HasAttribute("disabled"));
        Assert.NotNull(cut.Find("button[title='Lemondásra jelölés']"));
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
        Assert.Single(cut.FindAll(".week-grid__cell input[type=checkbox]"));
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

        var toggleButton = cut.Find("button[title='Lemondásra jelölés']");
        toggleButton.Click();

        Assert.Contains("1 nap</strong> kiválasztva lemondásra", cut.Markup);
        Assert.Contains("Lemondás megerősítése (1 nap)", cut.Markup);

        var undoButton = cut.Find("button[title='Lemondás visszavonása']");
        undoButton.Click();

        Assert.DoesNotContain("kiválasztva lemondásra", cut.Markup);
    }

    [Fact]
    public async Task Submitting_cancellations_sends_all_marked_dates_in_one_CancelMenuOrdersCommand_call()
    {
        // A naptár-rács hétvégi napokat sosem renderel (BuildWeeks csak hétfő-péntek oszlopot épít),
        // ezért a második naphoz nem elég "Today + 1 nap" — az szombatra eshet, és akkor a hozzá
        // tartozó "Lemondásra jelölés" gomb egyszerűen nem létezik a DOM-ban, a teszt pedig a
        // futtatás valódi hétnapjától függően hibázna. A NextWeekday mindkét dátumot a legközelebbi
        // (a mai nappal bezárólag) munkanapokra rögzíti, függetlenül attól, hányadik nap fut a teszt.
        var day1 = NextWeekday(Today);
        var day2 = NextWeekday(day1.AddDays(1));
        mediator.Register<GetOrderableDaysQuery, Result<IReadOnlyList<OrderableDayDto>>>(_ => Result.Success<IReadOnlyList<OrderableDayDto>>(
        [
            new OrderableDayDto(day1, false, true, "A", "Gulyásleves", ErrorCodes.AlreadyOrdered, null),
            new OrderableDayDto(day2, false, true, "B", "Húsleves", ErrorCodes.AlreadyOrdered, null),
        ]));

        CancelMenuOrdersCommand? sentCommand = null;
        mediator.Register<CancelMenuOrdersCommand, Result<BatchOrderResult>>(cmd =>
        {
            sentCommand = cmd;
            return Result.Success(new BatchOrderResult([new DayResult(day1, "A"), new DayResult(day2, "B")], []));
        });

        Render<MudDialogProvider>();
        var cut = Render<UserCalendar>();

        var daysToMark = cut.FindAll("button[title='Lemondásra jelölés']").Count;
        for (var i = 0; i < daysToMark; i++)
        {
            cut.Find("button[title='Lemondásra jelölés']").Click();
        }

        var submitButton = cut.FindAll("button").First(b => b.TextContent.Contains("Lemondás megerősítése"));
        await cut.InvokeAsync(() => submitButton.Click());

        Assert.NotNull(sentCommand);
        Assert.Equal(1, sentCommand!.TargetUserId);
        Assert.Equal(1, sentCommand.CancelledByUserId);
        Assert.Equal([day1, day2], sentCommand.Dates.OrderBy(d => d));
    }

    private static DateOnly NextWeekday(DateOnly date)
    {
        while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            date = date.AddDays(1);
        }

        return date;
    }

    [Fact]
    public async Task Successful_cancellation_with_nothing_skipped_clears_the_selection_and_shows_a_snackbar_instead_of_the_dialog()
    {
        mediator.Register<GetOrderableDaysQuery, Result<IReadOnlyList<OrderableDayDto>>>(_ => Result.Success<IReadOnlyList<OrderableDayDto>>(
        [
            new OrderableDayDto(Today, false, true, "A", "Gulyásleves", ErrorCodes.AlreadyOrdered, null),
        ]));
        mediator.Register<CancelMenuOrdersCommand, Result<BatchOrderResult>>(
            _ => Result.Success(new BatchOrderResult([new DayResult(Today, "A")], [])));

        var dialogProvider = Render<MudDialogProvider>();
        var snackbarProvider = Render<MudSnackbarProvider>();
        var cut = Render<UserCalendar>();

        var toggleButton = cut.Find("button[title='Lemondásra jelölés']");
        toggleButton.Click();

        var submitButton = cut.FindAll("button").First(b => b.TextContent.Contains("Lemondás megerősítése"));
        await cut.InvokeAsync(() => submitButton.Click());

        Assert.DoesNotContain("kiválasztva lemondásra", cut.Markup);
        Assert.DoesNotContain("Lemondás eredménye", dialogProvider.Markup);
        Assert.Contains("1 nap sikeresen lemondva", snackbarProvider.Markup);
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

        var toggleButton = cut.Find("button[title='Lemondásra jelölés']");
        toggleButton.Click();

        var submitButton = cut.FindAll("button").First(b => b.TextContent.Contains("Lemondás megerősítése"));
        await cut.InvokeAsync(() => submitButton.Click());

        Assert.Contains("A nap már le van zárva", provider.Markup);
    }

    [Fact]
    public async Task Picking_a_colleague_and_cancelling_as_an_admin_sends_their_id_as_TargetUserId_and_keeps_the_real_canceller_as_CancelledByUserId()
    {
        // A fixture felhasználója ADMIN, és ez itt már lényeges: idegen naptárban a lemondás
        // admin-jog (AC 3.2.8). A dolgozói ág külön tesztben van, ott a gomb létre sem jön.
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

        await PickColleagueAsync(cut, Colleague);

        var toggleButton = cut.Find("button[title='Lemondásra jelölés']");
        toggleButton.Click();

        var submitButton = cut.FindAll("button").First(b => b.TextContent.Contains("Lemondás megerősítése"));
        await cut.InvokeAsync(() => submitButton.Click());

        Assert.NotNull(sentCommand);
        Assert.Equal(2, sentCommand!.TargetUserId);
        Assert.Equal(1, sentCommand.CancelledByUserId);
    }

    // --- Kolléga-nézet dolgozóként: csak rendelés, csak előre (AC 3.1.9 / 3.2.7) ---

    private static readonly OrderingPeriodDto NextPeriod =
        new(2, "Következő időszak", Today.AddDays(31), Today.AddDays(60), DateTime.Today.AddDays(20), true, false);

    private static readonly OrderingPeriodDto ClosedPeriod =
        new(3, "Lezárt időszak", Today.AddDays(-90), Today.AddDays(-61), DateTime.Today.AddDays(-100), false, true);

    /// <summary>Dolgozó + a fixture három időszaka (kettő nyitott, egy lezárt).</summary>
    private void UseWorkerWithTwoOpenPeriods()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Teszt Dolgozó", isAdmin: false));
        mediator.Register<GetOrderingPeriodsQuery, IReadOnlyList<OrderingPeriodDto>>(_ => [Period, NextPeriod, ClosedPeriod]);
    }

    [Fact]
    public async Task A_worker_in_a_colleagues_calendar_gets_no_period_selector_and_no_way_to_cancel()
    {
        UseWorkerWithTwoOpenPeriods();
        mediator.Register<GetOrderableDaysQuery, Result<IReadOnlyList<OrderableDayDto>>>(_ => Result.Success<IReadOnlyList<OrderableDayDto>>(
        [
            new OrderableDayDto(Today, false, true, "A", "Gulyásleves", ErrorCodes.AlreadyOrdered, null),
        ]));

        var cut = Render<UserCalendar>();
        Assert.NotNull(cut.FindComponent<PeriodSelector>());
        Assert.NotEmpty(cut.FindAll("button[title='Lemondásra jelölés']"));

        await PickColleagueAsync(cut, Colleague);

        Assert.Empty(cut.FindComponents<PeriodSelector>());
        Assert.Empty(cut.FindAll("button[title='Lemondásra jelölés']"));
        Assert.DoesNotContain("Lemondásra jelölve", cut.Markup);
        Assert.DoesNotContain("vagy lemondasz", cut.Markup);
        Assert.Contains("csak rendelést adhatsz le", cut.Markup);
    }

    [Fact]
    public async Task A_worker_in_a_colleagues_calendar_loads_every_open_period_and_skips_the_closed_one()
    {
        UseWorkerWithTwoOpenPeriods();
        var requestedPeriodIds = new List<int>();
        mediator.Register<GetOrderableDaysQuery, Result<IReadOnlyList<OrderableDayDto>>>(q =>
        {
            requestedPeriodIds.Add(q.OrderingPeriodId);
            return Result.Success<IReadOnlyList<OrderableDayDto>>([]);
        });

        var cut = Render<UserCalendar>();
        Assert.Equal([Period.Id], requestedPeriodIds);

        requestedPeriodIds.Clear();
        await PickColleagueAsync(cut, Colleague);

        // Az időszakváltó helyett minden NYITOTT, még tartó időszak betöltődik egymás alá; a lezárt
        // időszak nem — abból a szerver úgysem adna vissza semmit.
        Assert.Equal([Period.Id, NextPeriod.Id], requestedPeriodIds);
        Assert.Contains(NextPeriod.Name, cut.Markup);
    }

    [Fact]
    public async Task A_worker_sees_a_non_orderable_day_in_a_colleagues_calendar_as_disabled_checkboxes()
    {
        UseWorkerWithTwoOpenPeriods();
        mediator.Register<GetOrderableDaysQuery, Result<IReadOnlyList<OrderableDayDto>>>(q =>
            Result.Success<IReadOnlyList<OrderableDayDto>>(q.OrderingPeriodId == Period.Id
                ? [new OrderableDayDto(Today, false, false, null, null, ErrorCodes.DeadlinePassed, null)]
                : []));
        mediator.Register<GetPeriodMenuQuery, Result<IReadOnlyList<DailyMenuDto>>>(_ => Result.Success<IReadOnlyList<DailyMenuDto>>(
        [
            new DailyMenuDto(Today, true, null, [new MenuVariantDto("A", "Gulyásleves", "Csirkepaprikás", 0)]),
        ]));

        var cut = Render<UserCalendar>();
        await PickColleagueAsync(cut, Colleague);

        // A nap létezik és látszik, mi lenne rajta — de nem lehet rákattintani.
        Assert.Contains("A menü — Gulyásleves", cut.Markup);
        Assert.Contains("A módosítási határidő lejárt", cut.Markup);
        var checkbox = Assert.Single(cut.FindAll(".week-grid__cell input[type=checkbox]"));
        Assert.True(checkbox.HasAttribute("disabled"));
    }

    [Fact]
    public async Task Days_picked_across_two_periods_are_sent_as_one_command_per_period()
    {
        UseWorkerWithTwoOpenPeriods();
        var dayInNextPeriod = NextPeriod.StartDate;
        mediator.Register<GetOrderableDaysQuery, Result<IReadOnlyList<OrderableDayDto>>>(q =>
            Result.Success<IReadOnlyList<OrderableDayDto>>(q.OrderingPeriodId == Period.Id
                ? [new OrderableDayDto(NextWeekday(Today), true, false, null, null, ErrorCodes.NoActiveOrder, null, MenuPortionHuf: 1400)]
                : [new OrderableDayDto(NextWeekday(dayInNextPeriod), true, false, null, null, ErrorCodes.NoActiveOrder, null, MenuPortionHuf: 1400)]));
        mediator.Register<GetPeriodMenuQuery, Result<IReadOnlyList<DailyMenuDto>>>(_ => Result.Success<IReadOnlyList<DailyMenuDto>>(
        [
            new DailyMenuDto(NextWeekday(Today), true, null, [new MenuVariantDto("A", "Gulyásleves", null, 0)]),
            new DailyMenuDto(NextWeekday(dayInNextPeriod), true, null, [new MenuVariantDto("A", "Gulyásleves", null, 0)]),
        ]));

        var sentCommands = new List<PlacePeriodOrderCommand>();
        mediator.Register<PlacePeriodOrderCommand, Result<BatchOrderResult>>(cmd =>
        {
            sentCommands.Add(cmd);
            return Result.Success(new BatchOrderResult([new DayResult(cmd.Days[0].Date, "A")], []));
        });

        Render<MudDialogProvider>();
        var snackbarProvider = Render<MudSnackbarProvider>();
        var cut = Render<UserCalendar>();
        await PickColleagueAsync(cut, Colleague);

        foreach (var checkbox in cut.FindAll("input[type=checkbox]:not([disabled])"))
        {
            checkbox.Change(true);
        }

        var submitButton = cut.FindAll("button").First(b => b.TextContent.Contains("Rendelés leadása"));
        await cut.InvokeAsync(() => submitButton.Click());

        // A PlacePeriodOrderCommand egy időszakra szól, a kijelölés viszont kettőt érint — ezért megy
        // két parancs, és az eredményeik egyetlen összesítésbe fűződnek.
        Assert.Equal(2, sentCommands.Count);
        Assert.Equal([Period.Id, NextPeriod.Id], sentCommands.Select(c => c.OrderingPeriodId));
        Assert.All(sentCommands, c => Assert.Equal(Colleague.Id, c.TargetUserId));
        Assert.All(sentCommands, c => Assert.Equal(1, c.PlacedByUserId));
        Assert.Contains("2 nap sikeresen megrendelve", snackbarProvider.Markup);
    }

    [Fact]
    public void Shows_a_balance_notice_when_the_user_has_a_positive_balance()
    {
        mediator.Register<GetMyBalanceQuery, Result<int>>(_ => Result.Success(1400));
        mediator.Register<GetOrderableDaysQuery, Result<IReadOnlyList<OrderableDayDto>>>(_ => Result.Success<IReadOnlyList<OrderableDayDto>>([]));

        var cut = Render<UserCalendar>();

        Assert.Contains("1\u00A0400 Ft", cut.Markup);
        Assert.Contains("nem térül vissza készpénzben", cut.Markup);
    }

    [Fact]
    public void Does_not_show_a_balance_notice_when_the_balance_is_zero()
    {
        mediator.Register<GetOrderableDaysQuery, Result<IReadOnlyList<OrderableDayDto>>>(_ => Result.Success<IReadOnlyList<OrderableDayDto>>([]));

        var cut = Render<UserCalendar>();

        Assert.DoesNotContain("nem térül vissza készpénzben", cut.Markup);
    }
}
