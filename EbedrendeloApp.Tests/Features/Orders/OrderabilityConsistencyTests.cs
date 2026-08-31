using EbedrendeloApp.Common.Calendar;
using EbedrendeloApp.Common.Services;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Calendar.GetOrderableDays;
using EbedrendeloApp.Features.Orders.CancelMenuOrders;
using EbedrendeloApp.Features.Orders.PlacePeriodOrder;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Tests.Features.Orders;

/// <summary>
/// Direct test of AC 1.7.4: for any given day, GetOrderableDaysQuery.Orderable/Cancellable must agree
/// with whether PlacePeriodOrderCommand/CancelMenuOrdersCommand would actually succeed on that same day
/// at the same instant — the query is a confirmation of the command's outcome, not a separate opinion.
/// </summary>
public class OrderabilityConsistencyTests : IDisposable
{
    private static readonly DateOnly Mon = new(2026, 8, 17);
    private static readonly DateOnly Tue = new(2026, 8, 18);
    private static readonly DateOnly Wed = new(2026, 8, 19);
    private static readonly DateOnly Thu = new(2026, 8, 20);
    private static readonly DateOnly Fri = new(2026, 8, 21);

    private static readonly DateTime Now = new(2026, 8, 17, 9, 0, 0); // Monday 09:00 — supplementary phase, deadline 11:00

    private readonly SqliteDbContextFactory dbFactory = new();

    private int periodId;
    private int userId;

    public void Dispose() => dbFactory.Dispose();

    private async Task SeedAsync()
    {
        await using var db = dbFactory.CreateDbContext();

        var period = new OrderingPeriod
        {
            Name = "Teszt időszak",
            StartDate = Mon,
            EndDate = Fri,
            OrderDeadline = new DateTime(2026, 8, 1, 10, 0, 0), // already passed — supplementary phase only
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

        var dish = new MenuDish { Kind = MenuDishKind.Leves, Name = "Gulyásleves" };
        db.MenuDishes.Add(dish);
        await db.SaveChangesAsync();

        foreach (var date in new[] { Mon, Tue, Wed, Thu, Fri })
        {
            var menu = new DailyMenu { Date = date, IsPublished = true };
            menu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "A", SoupName = "Gulyásleves", SoupDishId = dish.Id, SortOrder = 0 });
            db.DailyMenus.Add(menu);
        }

        await db.SaveChangesAsync();
    }

    private async Task SeedActiveOrderAsync(DateOnly date)
    {
        await using var db = dbFactory.CreateDbContext();
        var menu = await db.DailyMenus.Include(m => m.Variants).SingleAsync(m => m.Date == date);
        db.MenuOrders.Add(new MenuOrder
        {
            UserId = userId,
            Date = date,
            OrderingPeriodId = periodId,
            MenuVariantId = menu.Variants[0].Id,
            PriceHuf = 1400,
            Status = OrderStatus.Active,
            PlacedByUserId = userId,
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Cancellable_flag_agrees_with_whether_CancelMenuOrders_actually_succeeds()
    {
        await SeedAsync();
        await SeedActiveOrderAsync(Thu); // still within the deadline at Now
        await SeedActiveOrderAsync(Tue); // deadline already passed at Now

        var queryHandler = new GetOrderableDaysHandler(dbFactory, new FixedAppClock(Now), new WorkingDayCalculator());
        var queryResult = await queryHandler.Handle(new GetOrderableDaysQuery(periodId, userId), CancellationToken.None);
        var byDate = queryResult.Value!.ToDictionary(d => d.Date);

        Assert.True(byDate[Thu].Cancellable);
        Assert.False(byDate[Tue].Cancellable);

        var cancelHandler = new CancelMenuOrdersHandler(
            dbFactory, new FixedAppClock(Now), new WorkingDayCalculator(),
            new CreditService(), new NotificationService());
        var cancelResult = await cancelHandler.Handle(new CancelMenuOrdersCommand(userId, userId, [Thu, Tue]), CancellationToken.None);

        Assert.Contains(cancelResult.Value!.Succeeded, s => s.Date == Thu);
        var tueSkip = Assert.Single(cancelResult.Value.Skipped);
        Assert.Equal(Tue, tueSkip.Date);
        Assert.Equal(byDate[Tue].Reason, tueSkip.Reason);
    }

    [Fact]
    public async Task Orderable_flag_agrees_with_whether_PlacePeriodOrder_actually_succeeds()
    {
        await SeedAsync();
        // No existing orders: Fri is still within its deadline at Now, Wed's deadline already passed.

        var queryHandler = new GetOrderableDaysHandler(dbFactory, new FixedAppClock(Now), new WorkingDayCalculator());
        var queryResult = await queryHandler.Handle(new GetOrderableDaysQuery(periodId, userId), CancellationToken.None);
        var byDate = queryResult.Value!.ToDictionary(d => d.Date);

        Assert.True(byDate[Fri].Orderable);
        Assert.False(byDate[Wed].Orderable);

        var orderHandler = new PlacePeriodOrderHandler(dbFactory, new FixedAppClock(Now), new WorkingDayCalculator());
        var orderResult = await orderHandler.Handle(
            new PlacePeriodOrderCommand(userId, userId, periodId, [new DayOrderRequest(Fri, "A"), new DayOrderRequest(Wed, "A")]),
            CancellationToken.None);

        Assert.Contains(orderResult.Value!.Succeeded, s => s.Date == Fri);
        var wedSkip = Assert.Single(orderResult.Value.Skipped);
        Assert.Equal(Wed, wedSkip.Date);
        Assert.Equal(byDate[Wed].Reason, wedSkip.Reason);
    }
}
