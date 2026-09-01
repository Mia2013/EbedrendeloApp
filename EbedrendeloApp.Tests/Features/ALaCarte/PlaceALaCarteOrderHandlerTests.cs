using EbedrendeloApp.Common.Calendar;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.ALaCarte.PlaceALaCarteOrder;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Tests.Features.ALaCarte;

public class PlaceALaCarteOrderHandlerTests : IDisposable
{
    // 2026-08-17 is a Monday (same reference date used elsewhere in this suite).
    private static readonly DateOnly Mon = new(2026, 8, 17);
    private static readonly DateOnly Sat = new(2026, 8, 22);

    private readonly SqliteDbContextFactory dbFactory = new();

    private int userId;
    private int soupItemId;
    private int mainItemId;
    private int sideItemId;

    public void Dispose() => dbFactory.Dispose();

    private PlaceALaCarteOrderHandler CreateHandler(DateTime nowLocal)
        => new(dbFactory, new FixedAppClock(nowLocal), new WorkingDayCalculator());

    private async Task SeedAsync(bool withSoupOffer = true, int mainCapacity = 10, bool includeOrderingPeriod = true)
    {
        await using var db = dbFactory.CreateDbContext();

        if (includeOrderingPeriod)
        {
            db.OrderingPeriods.Add(new OrderingPeriod
            {
                Name = "Teszt időszak",
                StartDate = Mon,
                EndDate = Mon,
                OrderDeadline = new DateTime(2026, 8, 15, 10, 0, 0),
            });
        }

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

        var soup = new ALaCarteItem { Name = "Csontleves", Category = ALaCarteCategory.Leves, PriceHuf = 400 };
        var main = new ALaCarteItem { Name = "Rántott szelet", Category = ALaCarteCategory.Foetel, PriceHuf = 1500 };
        var side = new ALaCarteItem { Name = "Rizi-bizi", Category = ALaCarteCategory.Koret, PriceHuf = 500 };
        db.ALaCarteItems.AddRange(soup, main, side);
        await db.SaveChangesAsync();

        soupItemId = soup.Id;
        mainItemId = main.Id;
        sideItemId = side.Id;

        var offers = new List<ALaCarteDailyOffer>
        {
            new() { Date = Mon, ALaCarteItemId = main.Id, Capacity = mainCapacity, OrderedCount = 0 },
            new() { Date = Mon, ALaCarteItemId = side.Id, Capacity = 10, OrderedCount = 0 },
        };
        if (withSoupOffer)
        {
            offers.Add(new ALaCarteDailyOffer { Date = Mon, ALaCarteItemId = soup.Id, Capacity = int.MaxValue, OrderedCount = 0 });
        }
        db.ALaCarteDailyOffers.AddRange(offers);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Orders_a_main_dish_with_the_combined_soup_price_when_soup_is_offered_today()
    {
        await SeedAsync();
        var sut = CreateHandler(new DateTime(2026, 8, 17, 9, 0, 0));

        var result = await sut.Handle(new PlaceALaCarteOrderCommand(userId, [mainItemId]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var line = Assert.Single(result.Value!.Lines);
        Assert.True(line.IncludesSoup);
        Assert.Equal(1900, line.UnitPriceHuf); // 1500 (main) + 400 (soup)
        Assert.Equal(1900, result.Value.TotalHuf);
    }

    [Fact]
    public async Task Orders_a_side_dish_at_its_plain_price_without_soup_bundling()
    {
        await SeedAsync();
        var sut = CreateHandler(new DateTime(2026, 8, 17, 9, 0, 0));

        var result = await sut.Handle(new PlaceALaCarteOrderCommand(userId, [sideItemId]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var line = Assert.Single(result.Value!.Lines);
        Assert.False(line.IncludesSoup);
        Assert.Equal(500, line.UnitPriceHuf);
    }

    [Fact]
    public async Task Orders_a_main_dish_at_its_plain_price_when_no_soup_is_offered_today()
    {
        await SeedAsync(withSoupOffer: false);
        var sut = CreateHandler(new DateTime(2026, 8, 17, 9, 0, 0));

        var result = await sut.Handle(new PlaceALaCarteOrderCommand(userId, [mainItemId]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var line = Assert.Single(result.Value!.Lines);
        Assert.False(line.IncludesSoup);
        Assert.Equal(1500, line.UnitPriceHuf);
    }

    [Fact]
    public async Task Rejects_ordering_on_a_non_working_day()
    {
        await SeedAsync();
        var sut = CreateHandler(new DateTime(2026, 8, 22, 9, 0, 0)); // Sat

        var result = await sut.Handle(new PlaceALaCarteOrderCommand(userId, [sideItemId]), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NotWorkingDay, result.ErrorCode);
    }

    [Fact]
    public async Task Rejects_ordering_on_an_excluded_day()
    {
        await SeedAsync();
        await using (var db = dbFactory.CreateDbContext())
        {
            db.ExcludedDays.Add(new ExcludedDay { Date = Mon, Reason = "Teszt", CreatedByUserId = userId });
            await db.SaveChangesAsync();
        }
        var sut = CreateHandler(new DateTime(2026, 8, 17, 9, 0, 0));

        var result = await sut.Handle(new PlaceALaCarteOrderCommand(userId, [sideItemId]), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.DayExcluded, result.ErrorCode);
    }

    [Fact]
    public async Task Rejects_ordering_after_the_daily_deadline()
    {
        await SeedAsync();
        var sut = CreateHandler(new DateTime(2026, 8, 17, 11, 0, 0)); // after 10:30

        var result = await sut.Handle(new PlaceALaCarteOrderCommand(userId, [sideItemId]), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.DeadlinePassed, result.ErrorCode);
    }

    [Fact]
    public async Task Rejects_ordering_when_no_ordering_period_covers_today()
    {
        await SeedAsync(includeOrderingPeriod: false);
        var sut = CreateHandler(new DateTime(2026, 8, 17, 9, 0, 0));

        var result = await sut.Handle(new PlaceALaCarteOrderCommand(userId, [sideItemId]), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.OutsidePeriod, result.ErrorCode);
    }

    [Fact]
    public async Task Rejects_a_request_for_an_item_with_no_offer_today()
    {
        await SeedAsync();
        var sut = CreateHandler(new DateTime(2026, 8, 17, 9, 0, 0));

        var result = await sut.Handle(new PlaceALaCarteOrderCommand(userId, [999]), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.OfferNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Rejects_ordering_soup_directly()
    {
        await SeedAsync();
        var sut = CreateHandler(new DateTime(2026, 8, 17, 9, 0, 0));

        var result = await sut.Handle(new PlaceALaCarteOrderCommand(userId, [soupItemId]), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NotDirectlyOrderable, result.ErrorCode);
    }

    [Fact]
    public async Task Rejects_ordering_the_same_item_twice_in_one_day()
    {
        await SeedAsync();
        var sut = CreateHandler(new DateTime(2026, 8, 17, 9, 0, 0));
        await sut.Handle(new PlaceALaCarteOrderCommand(userId, [sideItemId]), CancellationToken.None);

        var result = await sut.Handle(new PlaceALaCarteOrderCommand(userId, [sideItemId]), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.AlreadyOrdered, result.ErrorCode);
    }

    [Fact]
    public async Task Rejects_ordering_a_sold_out_item()
    {
        await SeedAsync(mainCapacity: 0);
        var sut = CreateHandler(new DateTime(2026, 8, 17, 9, 0, 0));

        var result = await sut.Handle(new PlaceALaCarteOrderCommand(userId, [mainItemId]), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.OutOfStock, result.ErrorCode);
    }

    [Fact]
    public async Task Adding_a_second_item_on_a_later_call_accumulates_onto_the_same_order()
    {
        await SeedAsync();
        var sut = CreateHandler(new DateTime(2026, 8, 17, 9, 0, 0));
        await sut.Handle(new PlaceALaCarteOrderCommand(userId, [sideItemId]), CancellationToken.None);

        var result = await sut.Handle(new PlaceALaCarteOrderCommand(userId, [mainItemId]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        await using var db = dbFactory.CreateDbContext();
        var order = await db.ALaCarteOrders.Include(o => o.Lines).SingleAsync(o => o.UserId == userId && o.Date == Mon);
        Assert.Equal(2, order.Lines.Count);
        Assert.Equal(500 + 1900, order.TotalHuf);
    }

    [Fact]
    public async Task Two_concurrent_orders_for_the_last_unit_never_both_succeed()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"ebedrendelo-alacarte-race-{Guid.NewGuid():N}.db");
        using var factoryA = new FileSqliteDbContextFactory(dbPath, ensureCreated: true);
        using var factoryB = new FileSqliteDbContextFactory(dbPath, ensureCreated: false);

        int userA, userB, itemId;
        await using (var db = factoryA.CreateDbContext())
        {
            db.OrderingPeriods.Add(new OrderingPeriod
            {
                Name = "Verseny teszt",
                StartDate = Mon,
                EndDate = Mon,
                OrderDeadline = new DateTime(2026, 8, 15, 10, 0, 0),
            });
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

            var a = new User { UserId = 1, UserName = "a", RoleId = role.Id };
            var b = new User { UserId = 2, UserName = "b", RoleId = role.Id };
            db.Users.AddRange(a, b);
            await db.SaveChangesAsync();
            userA = a.Id;
            userB = b.Id;

            var item = new ALaCarteItem { Name = "Rizi-bizi", Category = ALaCarteCategory.Koret, PriceHuf = 500 };
            db.ALaCarteItems.Add(item);
            await db.SaveChangesAsync();
            itemId = item.Id;

            db.ALaCarteDailyOffers.Add(new ALaCarteDailyOffer { Date = Mon, ALaCarteItemId = item.Id, Capacity = 1, OrderedCount = 0 });
            await db.SaveChangesAsync();
        }

        var clock = new FixedAppClock(new DateTime(2026, 8, 17, 9, 0, 0));
        var handlerA = new PlaceALaCarteOrderHandler(factoryA, clock, new WorkingDayCalculator());
        var handlerB = new PlaceALaCarteOrderHandler(factoryB, clock, new WorkingDayCalculator());

        var results = await Task.WhenAll(
            handlerA.Handle(new PlaceALaCarteOrderCommand(userA, [itemId]), CancellationToken.None),
            handlerB.Handle(new PlaceALaCarteOrderCommand(userB, [itemId]), CancellationToken.None));

        Assert.Equal(1, results.Count(r => r.IsSuccess));
        Assert.Equal(1, results.Count(r => !r.IsSuccess && r.ErrorCode == ErrorCodes.OutOfStock));

        await using var verifyDb = factoryA.CreateDbContext();
        var orderedCount = await verifyDb.ALaCarteDailyOffers.Where(o => o.ALaCarteItemId == itemId && o.Date == Mon).Select(o => o.OrderedCount).SingleAsync();
        Assert.Equal(1, orderedCount);
    }
}
