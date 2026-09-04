using EbedrendeloApp.Common.Calendar;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.ALaCarte.CancelALaCarteOrderLine;
using EbedrendeloApp.Features.ALaCarte.PlaceALaCarteOrder;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Tests.Features.ALaCarte;

public class CancelALaCarteOrderLineHandlerTests : IDisposable
{
    // 2026-08-17 is a Monday (same reference date used elsewhere in this suite).
    private static readonly DateOnly Mon = new(2026, 8, 17);
    private static readonly DateOnly Sat = new(2026, 8, 22);

    private readonly SqliteDbContextFactory dbFactory = new();

    private int userId;
    private int mainItemId;
    private int sideItemId;

    public void Dispose() => dbFactory.Dispose();

    private CancelALaCarteOrderLineHandler CreateHandler(DateTime nowLocal)
        => new(dbFactory, new FixedAppClock(nowLocal), new WorkingDayCalculator());

    private PlaceALaCarteOrderHandler CreatePlaceHandler(DateTime nowLocal)
        => new(dbFactory, new FixedAppClock(nowLocal), new WorkingDayCalculator());

    private async Task SeedAsync()
    {
        await using var db = dbFactory.CreateDbContext();

        db.OrderingPeriods.Add(new OrderingPeriod
        {
            Name = "Teszt időszak",
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

        var user = new User { UserId = 1, UserName = "u1", RoleId = role.Id };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        userId = user.Id;

        var main = new ALaCarteItem { Name = "Rántott szelet", Category = ALaCarteCategory.Foetel, PriceHuf = 1500 };
        var side = new ALaCarteItem { Name = "Rizi-bizi", Category = ALaCarteCategory.Koret, PriceHuf = 500 };
        db.ALaCarteItems.AddRange(main, side);
        await db.SaveChangesAsync();

        mainItemId = main.Id;
        sideItemId = side.Id;

        db.ALaCarteDailyOffers.AddRange(
            new ALaCarteDailyOffer { Date = Mon, ALaCarteItemId = main.Id, Capacity = 10, OrderedCount = 0 },
            new ALaCarteDailyOffer { Date = Mon, ALaCarteItemId = side.Id, Capacity = 10, OrderedCount = 0 });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Cancels_an_ordered_line_and_releases_the_reserved_stock()
    {
        await SeedAsync();
        await CreatePlaceHandler(new DateTime(2026, 8, 17, 9, 0, 0))
            .Handle(new PlaceALaCarteOrderCommand(userId, [sideItemId]), CancellationToken.None);

        var sut = CreateHandler(new DateTime(2026, 8, 17, 9, 30, 0));
        var result = await sut.Handle(new CancelALaCarteOrderLineCommand(userId, sideItemId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        await using var db = dbFactory.CreateDbContext();
        var order = await db.ALaCarteOrders.Include(o => o.Lines).SingleAsync(o => o.UserId == userId && o.Date == Mon);
        Assert.Empty(order.Lines);
        Assert.Equal(0, order.TotalHuf);
        var offer = await db.ALaCarteDailyOffers.SingleAsync(o => o.ALaCarteItemId == sideItemId && o.Date == Mon);
        Assert.Equal(0, offer.OrderedCount);
    }

    [Fact]
    public async Task Rejects_canceling_an_item_that_was_never_ordered_today()
    {
        await SeedAsync();
        var sut = CreateHandler(new DateTime(2026, 8, 17, 9, 0, 0));

        var result = await sut.Handle(new CancelALaCarteOrderLineCommand(userId, sideItemId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Rejects_canceling_after_the_daily_deadline()
    {
        await SeedAsync();
        await CreatePlaceHandler(new DateTime(2026, 8, 17, 9, 0, 0))
            .Handle(new PlaceALaCarteOrderCommand(userId, [sideItemId]), CancellationToken.None);

        var sut = CreateHandler(new DateTime(2026, 8, 17, 11, 0, 0)); // after 10:30
        var result = await sut.Handle(new CancelALaCarteOrderLineCommand(userId, sideItemId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.DeadlinePassed, result.ErrorCode);
    }

    [Fact]
    public async Task Rejects_canceling_on_a_non_working_day()
    {
        await SeedAsync();
        var sut = CreateHandler(new DateTime(2026, 8, 22, 9, 0, 0)); // Sat

        var result = await sut.Handle(new CancelALaCarteOrderLineCommand(userId, sideItemId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NotWorkingDay, result.ErrorCode);
    }

    [Fact]
    public async Task Rejects_canceling_on_an_excluded_day()
    {
        await SeedAsync();
        await using (var db = dbFactory.CreateDbContext())
        {
            db.ExcludedDays.Add(new ExcludedDay { Date = Mon, Reason = "Teszt", CreatedByUserId = userId });
            await db.SaveChangesAsync();
        }
        var sut = CreateHandler(new DateTime(2026, 8, 17, 9, 0, 0));

        var result = await sut.Handle(new CancelALaCarteOrderLineCommand(userId, sideItemId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.DayExcluded, result.ErrorCode);
    }

    [Fact]
    public async Task After_canceling_a_different_item_in_the_same_category_can_be_ordered()
    {
        await SeedAsync();
        int secondMainItemId;
        await using (var db = dbFactory.CreateDbContext())
        {
            var secondMain = new ALaCarteItem { Name = "Csirkemell", Category = ALaCarteCategory.Foetel, PriceHuf = 1600 };
            db.ALaCarteItems.Add(secondMain);
            await db.SaveChangesAsync();
            secondMainItemId = secondMain.Id;

            db.ALaCarteDailyOffers.Add(new ALaCarteDailyOffer { Date = Mon, ALaCarteItemId = secondMain.Id, Capacity = 10, OrderedCount = 0 });
            await db.SaveChangesAsync();
        }

        var now = new DateTime(2026, 8, 17, 9, 0, 0);
        await CreatePlaceHandler(now).Handle(new PlaceALaCarteOrderCommand(userId, [mainItemId]), CancellationToken.None);

        var cancelResult = await CreateHandler(now).Handle(new CancelALaCarteOrderLineCommand(userId, mainItemId), CancellationToken.None);
        Assert.True(cancelResult.IsSuccess);

        var placeResult = await CreatePlaceHandler(now).Handle(new PlaceALaCarteOrderCommand(userId, [secondMainItemId]), CancellationToken.None);

        Assert.True(placeResult.IsSuccess);
    }
}
