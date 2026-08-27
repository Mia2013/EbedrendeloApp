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
    private readonly GetOrderableDaysHandler sut;

    private int periodId;
    private int userId;

    public GetOrderableDaysHandlerTests()
    {
        sut = new GetOrderableDaysHandler(dbFactory, clock, new WorkingDayCalculator());
    }

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

        var menu = new DailyMenu { Date = closedPeriod.StartDate, IsPublished = true };
        menu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "A", Name = "Gulyásleves", SortOrder = 0 });
        db.DailyMenus.Add(menu);
        await db.SaveChangesAsync();

        var result = await sut.Handle(new GetOrderableDaysQuery(closedPeriod.Id, user.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var day = Assert.Single(result.Value!);
        Assert.False(day.Orderable);
        Assert.False(day.Cancellable);
        Assert.Equal(ErrorCodes.PeriodClosed, day.Reason);
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

        db.ExcludedDays.Add(new ExcludedDay { Date = ExcludedDay, Reason = "Karbantartás", CreatedByUserId = user.Id });

        var orderedDayMenu = new DailyMenu { Date = AlreadyOrderedDay, IsPublished = true };
        orderedDayMenu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "A", Name = "Gulyásleves", SortOrder = 0 });
        db.DailyMenus.Add(orderedDayMenu);

        var orderableDayMenu = new DailyMenu { Date = OrderableDay, IsPublished = true };
        orderableDayMenu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "A", Name = "Rántott szelet", SortOrder = 0 });
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
