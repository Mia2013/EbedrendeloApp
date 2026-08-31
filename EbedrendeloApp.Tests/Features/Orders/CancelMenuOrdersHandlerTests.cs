using EbedrendeloApp.Common.Calendar;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Services;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Orders.CancelMenuOrders;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Tests.Features.Orders;

public class CancelMenuOrdersHandlerTests : IDisposable
{
    // Same worked example as 01-szerver-architektura.md 3.1: Thursday's ChangeDeadline is the
    // preceding Monday at 11:00 (3 working days back).
    private static readonly DateOnly Mon = new(2026, 8, 17);
    private static readonly DateOnly Tue = new(2026, 8, 18);
    private static readonly DateOnly Thu = new(2026, 8, 20);

    private readonly SqliteDbContextFactory dbFactory = new();

    private int periodId;
    private int userId;

    public void Dispose() => dbFactory.Dispose();

    private CancelMenuOrdersHandler CreateHandler(DateTime nowLocal)
        => new(dbFactory, new FixedAppClock(nowLocal), new WorkingDayCalculator(), new CreditService(), new NotificationService());

    private async Task SeedAsync(bool periodIsOpen = true, int changeDeadlineWorkingDays = 3)
    {
        await using var db = dbFactory.CreateDbContext();

        var period = new OrderingPeriod
        {
            Name = "Teszt időszak",
            StartDate = Mon,
            EndDate = new DateOnly(2026, 8, 21),
            OrderDeadline = new DateTime(2026, 8, 1, 10, 0, 0),
            IsOpen = periodIsOpen,
        };
        db.OrderingPeriods.Add(period);

        db.AppSettings.Add(new AppSetting
        {
            MenuPortionHuf = 1400,
            ChangeDeadlineWorkingDays = changeDeadlineWorkingDays,
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

        foreach (var date in new[] { Mon, Tue, Thu })
        {
            var menu = new DailyMenu { Date = date, IsPublished = true };
            menu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "A", SoupName = "Gulyásleves", SoupDishId = dish.Id, SortOrder = 0 });
            db.DailyMenus.Add(menu);
        }

        await db.SaveChangesAsync();
    }

    private async Task<int> SeedActiveOrderAsync(DateOnly date)
    {
        await using var db = dbFactory.CreateDbContext();
        var menu = await db.DailyMenus.Include(m => m.Variants).SingleAsync(m => m.Date == date);

        var order = new MenuOrder
        {
            UserId = userId,
            Date = date,
            OrderingPeriodId = periodId,
            MenuVariantId = menu.Variants[0].Id,
            PriceHuf = 1400,
            Status = OrderStatus.Active,
            PlacedByUserId = userId,
        };
        db.MenuOrders.Add(order);
        await db.SaveChangesAsync();
        return order.Id;
    }

    [Fact]
    public async Task Cancels_an_order_within_the_change_deadline_and_issues_credit()
    {
        await SeedAsync();
        var orderId = await SeedActiveOrderAsync(Thu);
        var sut = CreateHandler(new DateTime(2026, 8, 17, 9, 0, 0)); // Monday 09:00, deadline is 11:00

        var result = await sut.Handle(new CancelMenuOrdersCommand(userId, userId, [Thu]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Succeeded);
        Assert.Empty(result.Value.Skipped);

        await using var db = dbFactory.CreateDbContext();
        var order = await db.MenuOrders.SingleAsync(o => o.Id == orderId);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal(CancellationReason.ByUser, order.CancellationReason);
        Assert.Equal(userId, order.CancelledByUserId);

        var credit = await db.CreditEntries.SingleAsync(c => c.SourceMenuOrderId == orderId);
        Assert.Equal(1400, credit.AmountHuf);
        Assert.Equal(1400, credit.RemainingHuf);
        Assert.Equal(CreditEntryKind.CancellationCredit, credit.Kind);

        Assert.True(await db.UserNotifications.AnyAsync(n => n.RelatedMenuOrderId == orderId));
    }

    [Fact]
    public async Task Rejects_cancellation_past_the_change_deadline()
    {
        await SeedAsync();
        await SeedActiveOrderAsync(Thu);
        var sut = CreateHandler(new DateTime(2026, 8, 17, 12, 0, 0)); // Monday 12:00, past the 11:00 deadline

        var result = await sut.Handle(new CancelMenuOrdersCommand(userId, userId, [Thu]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var skip = Assert.Single(result.Value!.Skipped);
        Assert.Equal(ErrorCodes.DeadlinePassed, skip.Reason);
    }

    [Fact]
    public async Task Rejects_same_day_cancellation()
    {
        await SeedAsync();
        await SeedActiveOrderAsync(Thu);
        var sut = CreateHandler(new DateTime(2026, 8, 20, 9, 0, 0)); // Thursday itself

        var result = await sut.Handle(new CancelMenuOrdersCommand(userId, userId, [Thu]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var skip = Assert.Single(result.Value!.Skipped);
        Assert.Equal(ErrorCodes.DeadlinePassed, skip.Reason);
    }

    [Fact]
    public async Task Rejects_same_day_cancellation_even_if_deadline_setting_is_zero()
    {
        // Proves AC 3.2.2 is an explicit guard, not just an emergent effect of
        // ChangeDeadlineWorkingDays always being >= 1 — with the setting at 0, CanChange alone
        // would no longer reject a same-day change, so only the explicit date <= clock.Today
        // check in the handler can still be responsible for the rejection.
        await SeedAsync(changeDeadlineWorkingDays: 0);
        await SeedActiveOrderAsync(Thu);
        var sut = CreateHandler(new DateTime(2026, 8, 20, 9, 0, 0)); // Thursday itself

        var result = await sut.Handle(new CancelMenuOrdersCommand(userId, userId, [Thu]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var skip = Assert.Single(result.Value!.Skipped);
        Assert.Equal(ErrorCodes.DeadlinePassed, skip.Reason);
    }

    [Fact]
    public async Task Rejects_cancellation_when_the_period_is_closed()
    {
        await SeedAsync(periodIsOpen: false);
        await SeedActiveOrderAsync(Thu);
        var sut = CreateHandler(new DateTime(2026, 8, 17, 9, 0, 0)); // well within the deadline otherwise

        var result = await sut.Handle(new CancelMenuOrdersCommand(userId, userId, [Thu]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var skip = Assert.Single(result.Value!.Skipped);
        Assert.Equal(ErrorCodes.PeriodClosed, skip.Reason);
    }

    [Fact]
    public async Task Rejects_cancellation_when_the_kitchen_already_closed_the_day()
    {
        await SeedAsync();
        await SeedActiveOrderAsync(Thu);
        await using (var db = dbFactory.CreateDbContext())
        {
            db.KitchenClosures.Add(new KitchenClosure { Date = Thu, ClosedByUserId = userId, TotalPortions = 1 });
            await db.SaveChangesAsync();
        }

        var sut = CreateHandler(new DateTime(2026, 8, 17, 9, 0, 0));
        var result = await sut.Handle(new CancelMenuOrdersCommand(userId, userId, [Thu]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var skip = Assert.Single(result.Value!.Skipped);
        Assert.Equal(ErrorCodes.DayClosed, skip.Reason);
    }

    [Fact]
    public async Task Rejects_when_there_is_no_active_order_for_the_date()
    {
        await SeedAsync();
        var sut = CreateHandler(new DateTime(2026, 8, 17, 9, 0, 0));

        var result = await sut.Handle(new CancelMenuOrdersCommand(userId, userId, [Thu]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var skip = Assert.Single(result.Value!.Skipped);
        Assert.Equal(ErrorCodes.NoActiveOrder, skip.Reason);
    }

    [Fact]
    public async Task Batch_cancel_reports_partial_success()
    {
        await SeedAsync();
        await SeedActiveOrderAsync(Thu);
        await SeedActiveOrderAsync(Tue);
        var sut = CreateHandler(new DateTime(2026, 8, 17, 9, 0, 0)); // Monday 09:00 — Thu still changeable, Tue's deadline already passed

        var result = await sut.Handle(new CancelMenuOrdersCommand(userId, userId, [Thu, Tue]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(Thu, Assert.Single(result.Value!.Succeeded).Date);
        var skip = Assert.Single(result.Value.Skipped);
        Assert.Equal(Tue, skip.Date);
        Assert.Equal(ErrorCodes.DeadlinePassed, skip.Reason);
    }
}
