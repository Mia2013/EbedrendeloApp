using EbedrendeloApp.Common.Calendar;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Orders.PlacePeriodOrder;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Tests.Features.Orders;

public class PlacePeriodOrderHandlerTests : IDisposable
{
    // 2026-08-17 is a Monday (same reference date used by GetOrderableDaysHandlerTests).
    private static readonly DateOnly Mon = new(2026, 8, 17);
    private static readonly DateOnly Tue = new(2026, 8, 18);
    private static readonly DateOnly Wed = new(2026, 8, 19);
    private static readonly DateOnly Thu = new(2026, 8, 20);
    private static readonly DateOnly Fri = new(2026, 8, 21);
    private static readonly DateOnly Sat = new(2026, 8, 22);
    private static readonly DateOnly NextMon = new(2026, 8, 24); // workday, intentionally left without a DailyMenu

    private readonly SqliteDbContextFactory dbFactory = new();

    private int periodId;
    private int userId;
    private int otherUserId;

    public void Dispose() => dbFactory.Dispose();

    private PlacePeriodOrderHandler CreateHandler(DateTime nowLocal)
        => new(dbFactory, new FixedAppClock(nowLocal), new WorkingDayCalculator());

    private async Task SeedAsync()
    {
        await using var db = dbFactory.CreateDbContext();

        var period = new OrderingPeriod
        {
            Name = "Teszt időszak",
            StartDate = Mon,
            EndDate = NextMon,
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
        var otherUser = new User { UserId = 2, UserName = "u2", RoleId = role.Id };
        db.Users.AddRange(user, otherUser);
        await db.SaveChangesAsync();

        userId = user.Id;
        otherUserId = otherUser.Id;
        periodId = period.Id;

        var dish = new MenuDish { Kind = MenuDishKind.Leves, Name = "Gulyásleves" };
        db.MenuDishes.Add(dish);
        await db.SaveChangesAsync();

        foreach (var date in new[] { Mon, Tue, Wed, Thu, Fri })
        {
            var menu = new DailyMenu { Date = date, IsPublished = true };
            menu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "A", SoupName = "Gulyásleves", SoupDishId = dish.Id, SortOrder = 0 });
            db.DailyMenus.Add(menu);
        }

        // NextMon (2026-08-24) intentionally has no DailyMenu — covers ErrorCodes.MenuNotPublished.

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Phase_A_orders_any_published_workday_without_a_throughput_requirement()
    {
        await SeedAsync();
        var sut = CreateHandler(new DateTime(2026, 8, 10, 9, 0, 0)); // before OrderDeadline

        var result = await sut.Handle(
            new PlacePeriodOrderCommand(userId, userId, periodId, new[] { Mon, Tue, Wed, Thu, Fri }.Select(d => new DayOrderRequest(d, "A")).ToList()),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value!.Succeeded.Count);
        Assert.Empty(result.Value.Skipped);
    }

    [Fact]
    public async Task Phase_B_only_orders_days_within_the_change_deadline()
    {
        await SeedAsync();
        var sut = CreateHandler(new DateTime(2026, 8, 16, 12, 0, 0)); // after OrderDeadline, still within the period

        var result = await sut.Handle(
            new PlacePeriodOrderCommand(userId, userId, periodId, new[] { Mon, Tue, Wed, Thu, Fri }.Select(d => new DayOrderRequest(d, "A")).ToList()),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var succeededDates = result.Value!.Succeeded.Select(s => s.Date).OrderBy(d => d).ToList();
        var skippedByDate = result.Value.Skipped.ToDictionary(s => s.Date, s => s.Reason);

        Assert.Equal([Thu, Fri], succeededDates);
        Assert.Equal(ErrorCodes.DeadlinePassed, skippedByDate[Mon]);
        Assert.Equal(ErrorCodes.DeadlinePassed, skippedByDate[Tue]);
        Assert.Equal(ErrorCodes.DeadlinePassed, skippedByDate[Wed]);
    }

    [Fact]
    public async Task Rejects_a_day_that_already_has_an_active_order()
    {
        await SeedAsync();
        await using (var db = dbFactory.CreateDbContext())
        {
            var menu = await db.DailyMenus.Include(m => m.Variants).SingleAsync(m => m.Date == Mon);
            db.MenuOrders.Add(new MenuOrder
            {
                UserId = userId,
                Date = Mon,
                OrderingPeriodId = periodId,
                MenuVariantId = menu.Variants[0].Id,
                PriceHuf = 1400,
                Status = OrderStatus.Active,
                PlacedByUserId = userId,
            });
            await db.SaveChangesAsync();
        }

        var sut = CreateHandler(new DateTime(2026, 8, 10, 9, 0, 0));
        var result = await sut.Handle(new PlacePeriodOrderCommand(userId, userId, periodId, [new DayOrderRequest(Mon, "A")]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var skip = Assert.Single(result.Value!.Skipped);
        Assert.Equal(ErrorCodes.AlreadyOrdered, skip.Reason);
    }

    [Fact]
    public async Task Rejects_a_date_outside_the_period()
    {
        await SeedAsync();
        var sut = CreateHandler(new DateTime(2026, 8, 10, 9, 0, 0));
        var outsideDate = NextMon.AddDays(1);

        var result = await sut.Handle(new PlacePeriodOrderCommand(userId, userId, periodId, [new DayOrderRequest(outsideDate, "A")]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var skip = Assert.Single(result.Value!.Skipped);
        Assert.Equal(ErrorCodes.OutsidePeriod, skip.Reason);
    }

    [Fact]
    public async Task Rejects_a_weekend_date()
    {
        await SeedAsync();
        var sut = CreateHandler(new DateTime(2026, 8, 10, 9, 0, 0));

        var result = await sut.Handle(new PlacePeriodOrderCommand(userId, userId, periodId, [new DayOrderRequest(Sat, "A")]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var skip = Assert.Single(result.Value!.Skipped);
        Assert.Equal(ErrorCodes.NotWorkingDay, skip.Reason);
    }

    [Fact]
    public async Task Rejects_an_excluded_day()
    {
        await SeedAsync();
        await using (var db = dbFactory.CreateDbContext())
        {
            db.ExcludedDays.Add(new ExcludedDay { Date = Wed, Reason = "Karbantartás", CreatedByUserId = userId });
            await db.SaveChangesAsync();
        }

        var sut = CreateHandler(new DateTime(2026, 8, 10, 9, 0, 0));
        var result = await sut.Handle(new PlacePeriodOrderCommand(userId, userId, periodId, [new DayOrderRequest(Wed, "A")]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var skip = Assert.Single(result.Value!.Skipped);
        Assert.Equal(ErrorCodes.DayExcluded, skip.Reason);
    }

    [Fact]
    public async Task Rejects_a_day_the_kitchen_already_closed()
    {
        await SeedAsync();
        await using (var db = dbFactory.CreateDbContext())
        {
            db.KitchenClosures.Add(new KitchenClosure { Date = Wed, ClosedByUserId = userId, TotalPortions = 0 });
            await db.SaveChangesAsync();
        }

        var sut = CreateHandler(new DateTime(2026, 8, 10, 9, 0, 0));
        var result = await sut.Handle(new PlacePeriodOrderCommand(userId, userId, periodId, [new DayOrderRequest(Wed, "A")]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var skip = Assert.Single(result.Value!.Skipped);
        Assert.Equal(ErrorCodes.DayClosed, skip.Reason);
    }

    [Fact]
    public async Task Rejects_a_workday_without_a_published_menu()
    {
        await SeedAsync();
        var sut = CreateHandler(new DateTime(2026, 8, 10, 9, 0, 0));

        var result = await sut.Handle(new PlacePeriodOrderCommand(userId, userId, periodId, [new DayOrderRequest(NextMon, "A")]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var skip = Assert.Single(result.Value!.Skipped);
        Assert.Equal(ErrorCodes.MenuNotPublished, skip.Reason);
    }

    [Fact]
    public async Task Rejects_an_unknown_variant_code()
    {
        await SeedAsync();
        var sut = CreateHandler(new DateTime(2026, 8, 10, 9, 0, 0));

        var result = await sut.Handle(new PlacePeriodOrderCommand(userId, userId, periodId, [new DayOrderRequest(Mon, "Z")]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var skip = Assert.Single(result.Value!.Skipped);
        Assert.Equal(ErrorCodes.InvalidVariantCode, skip.Reason);
    }

    [Fact]
    public async Task Snapshots_the_price_from_AppSetting_MenuPortionHuf()
    {
        await SeedAsync();
        var sut = CreateHandler(new DateTime(2026, 8, 10, 9, 0, 0));

        var result = await sut.Handle(new PlacePeriodOrderCommand(userId, userId, periodId, [new DayOrderRequest(Mon, "A")]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Succeeded);

        await using var db = dbFactory.CreateDbContext();
        var order = await db.MenuOrders.SingleAsync(o => o.UserId == userId && o.Date == Mon);
        Assert.Equal(1400, order.PriceHuf);
    }

    [Fact]
    public async Task Placing_an_order_on_behalf_of_someone_else_records_both_users()
    {
        await SeedAsync();
        var sut = CreateHandler(new DateTime(2026, 8, 10, 9, 0, 0));

        var result = await sut.Handle(new PlacePeriodOrderCommand(userId, otherUserId, periodId, [new DayOrderRequest(Mon, "A")]), CancellationToken.None);

        Assert.True(result.IsSuccess);

        await using var db = dbFactory.CreateDbContext();
        var order = await db.MenuOrders.SingleAsync(o => o.Date == Mon);
        Assert.Equal(userId, order.UserId);
        Assert.Equal(otherUserId, order.PlacedByUserId);
    }

    [Fact]
    public async Task Partial_success_persists_the_succeeded_days_even_though_others_are_skipped()
    {
        await SeedAsync();
        var sut = CreateHandler(new DateTime(2026, 8, 16, 12, 0, 0));

        var result = await sut.Handle(
            new PlacePeriodOrderCommand(userId, userId, periodId, new[] { Mon, Thu }.Select(d => new DayOrderRequest(d, "A")).ToList()),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Succeeded);
        Assert.Single(result.Value.Skipped);

        await using var db = dbFactory.CreateDbContext();
        Assert.True(await db.MenuOrders.AnyAsync(o => o.Date == Thu));
        Assert.False(await db.MenuOrders.AnyAsync(o => o.Date == Mon));
    }

    [Fact]
    public async Task Two_concurrent_requests_for_the_same_user_and_date_never_double_book_the_day()
    {
        // This test needs two genuinely independent connections racing for the DB's filtered unique
        // index on (UserId, Date) WHERE Status = Active — the class-level `dbFactory` shares a single
        // connection across every context it creates, which is fine for one handler's sequential
        // awaits but not for two handlers actually running at once. So this test uses its own
        // file-backed Sqlite databases instead. It is inherently more timing-sensitive than the rest
        // of this suite (SQLite serializes writers at the file level), so if it ever proves flaky in
        // CI, that is a signal to look at it in isolation rather than at the fix it protects.
        var dbPath = Path.Combine(Path.GetTempPath(), $"ebedrendelo-race-{Guid.NewGuid():N}.db");
        using var factoryA = new FileSqliteDbContextFactory(dbPath, ensureCreated: true);
        using var factoryB = new FileSqliteDbContextFactory(dbPath, ensureCreated: false);

        int raceUserId;
        int racePeriodId;
        await using (var db = factoryA.CreateDbContext())
        {
            var period = new OrderingPeriod
            {
                Name = "Verseny teszt",
                StartDate = Mon,
                EndDate = Fri,
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

            var dish = new MenuDish { Kind = MenuDishKind.Leves, Name = "Gulyásleves" };
            db.MenuDishes.Add(dish);
            await db.SaveChangesAsync();

            var menu = new DailyMenu { Date = Mon, IsPublished = true };
            menu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "A", SoupName = "Gulyásleves", SoupDishId = dish.Id, SortOrder = 0 });
            db.DailyMenus.Add(menu);
            await db.SaveChangesAsync();

            racePeriodId = period.Id;
            raceUserId = user.Id;
        }

        var clock = new FixedAppClock(new DateTime(2026, 8, 10, 9, 0, 0)); // before OrderDeadline
        var handlerA = new PlacePeriodOrderHandler(factoryA, clock, new WorkingDayCalculator());
        var handlerB = new PlacePeriodOrderHandler(factoryB, clock, new WorkingDayCalculator());
        var command = new PlacePeriodOrderCommand(raceUserId, raceUserId, racePeriodId, [new DayOrderRequest(Mon, "A")]);

        // The point of the fix under test: neither call may let a raw DbUpdateException escape, even
        // though both handlers' in-app pre-checks can see an empty `userOrders` set at the same time.
        var results = await Task.WhenAll(handlerA.Handle(command, CancellationToken.None), handlerB.Handle(command, CancellationToken.None));

        var succeededCount = results.Count(r => r.IsSuccess && r.Value!.Succeeded.Count == 1);
        Assert.Equal(1, succeededCount);

        await using var verifyDb = factoryA.CreateDbContext();
        var orderCount = await verifyDb.MenuOrders.CountAsync(o => o.UserId == raceUserId && o.Date == Mon && o.Status == OrderStatus.Active);
        Assert.Equal(1, orderCount);
    }
}
