using EbedrendeloApp.Common.Calendar;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Calendar.GetOrderableDays;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Tests.Features.Calendar;

public class GetOrderableDaysHandlerTests : IDisposable
{
    private static readonly DateOnly ExcludedDay = new(2026, 8, 17); // Monday
    private static readonly DateOnly UnpublishedDay = new(2026, 8, 18); // Tuesday
    private static readonly DateOnly AlreadyOrderedDay = new(2026, 8, 19); // Wednesday
    private static readonly DateOnly OrderableDay = new(2026, 8, 20); // Thursday

    private readonly SqliteDbContextFactory dbFactory = new();
    private readonly FixedAppClock clock = new(new DateTime(2026, 8, 10, 9, 0, 0));

    // A `sut` a SAJÁT naptárát nézi: a seed a felhasználó valódi Id-jére állítja ezt a fake-et, hogy a
    // handler `foreignView` ága ne lépjen be. Az idegen nézetet a SutFor(...) példányai fedik.
    private readonly FakeCurrentUser currentUser = new(1, "Teszt Dolgozó", isAdmin: false);
    private readonly GetOrderableDaysHandler sut;

    private int periodId;
    private int userId;

    public GetOrderableDaysHandlerTests()
    {
        sut = new GetOrderableDaysHandler(dbFactory, clock, new WorkingDayCalculator(), currentUser);
    }

    /// <summary>Handler egy MÁSIK hívó nevében — a más nevében rendelés esete, ahol a lekérdezett
    /// felhasználó nem a hívó.</summary>
    private GetOrderableDaysHandler SutFor(bool isAdmin)
        => new(dbFactory, clock, new WorkingDayCalculator(), new FakeCurrentUser(9999, "Hívó", isAdmin));

    public void Dispose() => dbFactory.Dispose();

    [Fact]
    public async Task Reports_the_four_reasons_shown_on_the_worker_calendar_screen()
    {
        await SeedAsync();

        var result = await sut.Handle(new GetOrderableDaysQuery(periodId, userId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var days = result.Value!.ToDictionary(d => d.Date);

        var excluded = days[ExcludedDay];
        Assert.False(excluded.Orderable);
        Assert.False(excluded.Cancellable);
        Assert.Equal(ErrorCodes.DayExcluded, excluded.Reason);
        Assert.Equal("Karbantartás", excluded.ReasonDetail);

        var unpublished = days[UnpublishedDay];
        Assert.False(unpublished.Orderable);
        Assert.False(unpublished.Cancellable);
        Assert.Equal(ErrorCodes.MenuNotPublished, unpublished.Reason);

        var alreadyOrdered = days[AlreadyOrderedDay];
        Assert.False(alreadyOrdered.Orderable);
        Assert.True(alreadyOrdered.Cancellable);
        Assert.Equal(ErrorCodes.AlreadyOrdered, alreadyOrdered.Reason);
        Assert.Equal("A", alreadyOrdered.VariantCode);

        var orderable = days[OrderableDay];
        Assert.True(orderable.Orderable);
        Assert.False(orderable.Cancellable);
        Assert.Equal(ErrorCodes.NoActiveOrder, orderable.Reason);
    }

    [Fact]
    public async Task A_closed_period_is_never_orderable_or_cancellable_even_within_the_deadline_window()
    {
        await using var db = dbFactory.CreateDbContext();

        var closedPeriod = new OrderingPeriod
        {
            Name = "Zárt időszak",
            StartDate = new DateOnly(2026, 8, 17), // Monday
            EndDate = new DateOnly(2026, 8, 17),
            OrderDeadline = new DateTime(2026, 8, 20, 10, 0, 0), // still in the future relative to the fixed clock
            IsOpen = false,
        };
        db.OrderingPeriods.Add(closedPeriod);

        db.AppSettings.Add(new AppSetting
        {
            MenuPortionHuf = 1400,
            ChangeDeadlineWorkingDays = 3,
            ChangeDeadlineLocalTime = new TimeOnly(11, 0),
            ALaCarteOrderDeadlineLocalTime = new TimeOnly(10, 30),
        });

        var role = new Role { Name = "User" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var user = new User { UserId = 2, UserName = "u2", RoleId = role.Id };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        await currentUser.SwitchToAsync(user.Id);

        var dish = new MenuDish { Kind = MenuDishKind.Leves, Name = "Gulyásleves" };
        db.MenuDishes.Add(dish);
        await db.SaveChangesAsync();

        var menu = new DailyMenu { Date = closedPeriod.StartDate, IsPublished = true };
        menu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "A", SoupName = "Gulyásleves", SoupDishId = dish.Id, SortOrder = 0 });
        db.DailyMenus.Add(menu);
        await db.SaveChangesAsync();

        var result = await sut.Handle(new GetOrderableDaysQuery(closedPeriod.Id, user.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var day = Assert.Single(result.Value!);
        Assert.False(day.Orderable);
        Assert.False(day.Cancellable);
        Assert.Equal(ErrorCodes.PeriodClosed, day.Reason);
    }

    [Fact]
    public async Task A_kitchen_closed_day_is_not_orderable_but_becomes_orderable_again_after_a_reopen()
    {
        await SeedAsync();

        int closureId;
        await using (var db = dbFactory.CreateDbContext())
        {
            var closure = new KitchenClosure { Date = OrderableDay, ClosedByUserId = userId, TotalPortions = 0 };
            db.KitchenClosures.Add(closure);
            await db.SaveChangesAsync();
            closureId = closure.Id;
        }

        var whileClosed = await sut.Handle(new GetOrderableDaysQuery(periodId, userId), CancellationToken.None);
        var closedDay = whileClosed.Value!.Single(d => d.Date == OrderableDay);
        Assert.False(closedDay.Orderable);
        Assert.Equal(ErrorCodes.DayClosed, closedDay.Reason);

        await using (var db = dbFactory.CreateDbContext())
        {
            db.KitchenClosureReopenings.Add(new KitchenClosureReopening { KitchenClosureId = closureId, ReopenedByUserId = userId });
            await db.SaveChangesAsync();
        }

        var afterReopen = await sut.Handle(new GetOrderableDaysQuery(periodId, userId), CancellationToken.None);
        var reopenedDay = afterReopen.Value!.Single(d => d.Date == OrderableDay);
        Assert.True(reopenedDay.Orderable);
    }

    [Fact]
    public async Task An_invoiced_period_stays_orderable_and_cancellable()
    {
        // A számla kiállítása nem zárja le a naptárat: a B-fázisú pótrendelés és a 3 munkanapos szabály
        // szerinti lemondás a hónap alatt végig működik (01-szerver-architektura.md 3.1).
        await SeedAsync();
        await using (var db = dbFactory.CreateDbContext())
        {
            db.PeriodInvoices.Add(new PeriodInvoice
            {
                UserId = userId,
                OrderingPeriodId = periodId,
                SequenceNumber = 1,
                GrossHuf = 1400,
                CreditAppliedHuf = 0,
                PayableHuf = 1400,
                GeneratedAtUtc = new DateTime(2026, 8, 16, 9, 0, 0),
            });
            await db.SaveChangesAsync();
        }

        var result = await sut.Handle(new GetOrderableDaysQuery(periodId, userId), CancellationToken.None);
        var days = result.Value!.ToDictionary(d => d.Date);

        Assert.True(days[AlreadyOrderedDay].Cancellable);
        Assert.True(days[OrderableDay].Orderable);
    }

    [Fact]
    public async Task A_worker_looking_at_a_colleagues_calendar_gets_no_past_days_at_all()
    {
        // AC 3.1.9: a kolléga naptára nem böngészhető történet. A múltbeli napok nem „el vannak rejtve"
        // a felületen — be sem kerülnek a válaszba, tehát a kolléga korábbi rendeléseiről semmi nem
        // hagyja el a szervert. Adminnál viszont a teljes időszak marad.
        var (period, colleagueId) = await SeedPeriodAroundTodayAsync();

        var asAdmin = await SutFor(isAdmin: true).Handle(new GetOrderableDaysQuery(period, colleagueId), CancellationToken.None);
        var asWorker = await SutFor(isAdmin: false).Handle(new GetOrderableDaysQuery(period, colleagueId), CancellationToken.None);

        Assert.Contains(asAdmin.Value!, d => d.Date < clock.Today);
        Assert.NotEmpty(asWorker.Value!);
        Assert.All(asWorker.Value!, d => Assert.True(d.Date >= clock.Today, $"{d.Date} múltbeli nap került az idegen nézetbe"));
        Assert.Equal(clock.Today, asWorker.Value!.Min(d => d.Date));
    }

    [Fact]
    public async Task A_worker_cannot_open_a_colleagues_closed_period_at_all()
    {
        var (period, colleagueId) = await SeedPeriodAroundTodayAsync(isOpen: false);

        var result = await SutFor(isAdmin: false).Handle(new GetOrderableDaysQuery(period, colleagueId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.PeriodClosed, result.ErrorCode);
        Assert.Null(result.Value);

        // Az admin ugyanezt az időszakot továbbra is megnyithatja — csak nem lesz rajta rendelhető nap.
        var asAdmin = await SutFor(isAdmin: true).Handle(new GetOrderableDaysQuery(period, colleagueId), CancellationToken.None);
        Assert.True(asAdmin.IsSuccess);
    }

    /// <summary>A <see cref="SeedAsync"/> időszaka teljes egészében a rögzített óra UTÁN van, ezért az
    /// idegen nézet múlt-szűrését nem tudná megmutatni. Ez a seed olyan időszakot ad, ami átfogja a mai
    /// napot: 08.03–08.14, miközben „ma" 08.10.</summary>
    private async Task<(int PeriodId, int ColleagueId)> SeedPeriodAroundTodayAsync(bool isOpen = true)
    {
        await using var db = dbFactory.CreateDbContext();

        var period = new OrderingPeriod
        {
            Name = "Átfogó időszak",
            StartDate = new DateOnly(2026, 8, 3),
            EndDate = new DateOnly(2026, 8, 14),
            OrderDeadline = new DateTime(2026, 8, 1, 10, 0, 0),
            IsOpen = isOpen,
        };
        db.OrderingPeriods.Add(period);

        db.AppSettings.Add(new AppSetting
        {
            MenuPortionHuf = 1400,
            ChangeDeadlineWorkingDays = 3,
            ChangeDeadlineLocalTime = new TimeOnly(11, 0),
            ALaCarteOrderDeadlineLocalTime = new TimeOnly(10, 30),
        });

        var role = new Role { Name = "User" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var colleague = new User { UserId = 42, UserName = "kollega", RoleId = role.Id };
        db.Users.Add(colleague);
        await db.SaveChangesAsync();

        return (period.Id, colleague.Id);
    }

    private async Task SeedAsync()
    {
        await using var db = dbFactory.CreateDbContext();

        var period = new OrderingPeriod
        {
            Name = "Teszt időszak",
            StartDate = new DateOnly(2026, 8, 17),
            EndDate = new DateOnly(2026, 8, 21),
            OrderDeadline = new DateTime(2026, 8, 15, 10, 0, 0),
        };
        db.OrderingPeriods.Add(period);

        db.AppSettings.Add(new AppSetting
        {
            MenuPortionHuf = 1400,
            ChangeDeadlineWorkingDays = 3,
            ChangeDeadlineLocalTime = new TimeOnly(11, 0),
            ALaCarteOrderDeadlineLocalTime = new TimeOnly(10, 30),
        });

        var role = new Role { Name = "User" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var user = new User { UserId = 1, UserName = "u1", RoleId = role.Id };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        userId = user.Id;
        periodId = period.Id;
        await currentUser.SwitchToAsync(user.Id);

        db.ExcludedDays.Add(new ExcludedDay { Date = ExcludedDay, Reason = "Karbantartás", CreatedByUserId = user.Id });

        var dish1 = new MenuDish { Kind = MenuDishKind.Leves, Name = "Gulyásleves" };
        var dish2 = new MenuDish { Kind = MenuDishKind.Leves, Name = "Rántott szelet" };
        db.MenuDishes.AddRange(dish1, dish2);
        await db.SaveChangesAsync();

        var orderedDayMenu = new DailyMenu { Date = AlreadyOrderedDay, IsPublished = true };
        orderedDayMenu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "A", SoupName = "Gulyásleves", SoupDishId = dish1.Id, SortOrder = 0 });
        db.DailyMenus.Add(orderedDayMenu);

        var orderableDayMenu = new DailyMenu { Date = OrderableDay, IsPublished = true };
        orderableDayMenu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "A", SoupName = "Rántott szelet", SoupDishId = dish2.Id, SortOrder = 0 });
        db.DailyMenus.Add(orderableDayMenu);

        // UnpublishedDay intentionally has no DailyMenu at all.

        await db.SaveChangesAsync();

        db.MenuOrders.Add(new MenuOrder
        {
            UserId = user.Id,
            Date = AlreadyOrderedDay,
            OrderingPeriodId = period.Id,
            MenuVariantId = orderedDayMenu.Variants[0].Id,
            PriceHuf = 1400,
            Status = OrderStatus.Active,
            PlacedByUserId = user.Id,
        });

        await db.SaveChangesAsync();
    }
}
