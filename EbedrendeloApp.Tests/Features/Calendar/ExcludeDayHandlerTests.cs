using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Services;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Calendar.ExcludeDay;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Tests.Features.Calendar;

public class ExcludeDayHandlerTests : IDisposable
{
    private readonly SqliteDbContextFactory dbFactory = new();
    private readonly FixedAppClock clock = new(new DateTime(2026, 8, 17, 9, 0, 0));
    private readonly ExcludeDayHandler sut;

    private int userId;
    private int adminId;

    public ExcludeDayHandlerTests()
    {
        sut = new ExcludeDayHandler(dbFactory, clock, new CreditService(), new NotificationService());
    }

    public void Dispose() => dbFactory.Dispose();

    [Fact]
    public async Task Rejects_todays_date()
    {
        var result = await sut.Handle(new ExcludeDayCommand(clock.Today, "Indok", 1), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NotFutureDate, result.ErrorCode);
    }

    [Fact]
    public async Task Rejects_past_date()
    {
        var result = await sut.Handle(new ExcludeDayCommand(clock.Today.AddDays(-1), "Indok", 1), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NotFutureDate, result.ErrorCode);
    }

    [Fact]
    public async Task Cancels_active_invoiced_orders_and_issues_full_credit_and_notification()
    {
        var (periodId, orderId) = await SeedActiveOrderAsync(new DateOnly(2026, 8, 20), price: 1400, invoiced: true);

        var result = await sut.Handle(new ExcludeDayCommand(new DateOnly(2026, 8, 20), "Karbantartás", CreatedByUserId: adminId), CancellationToken.None);

        Assert.True(result.IsSuccess);

        await using var db = dbFactory.CreateDbContext();

        var order = await db.MenuOrders.SingleAsync(o => o.Id == orderId);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal(CancellationReason.DayExcluded, order.CancellationReason);

        var excludedDay = await db.ExcludedDays.SingleAsync(e => e.Date == new DateOnly(2026, 8, 20));
        Assert.Equal(order.CancelledByExcludedDayId, excludedDay.Id);

        var credit = await db.CreditEntries.SingleAsync(c => c.SourceMenuOrderId == orderId);
        Assert.Equal(1400, credit.AmountHuf);
        Assert.Equal(1400, credit.RemainingHuf);
        Assert.Equal(CreditEntryKind.CancellationCredit, credit.Kind);

        var notification = await db.UserNotifications.SingleAsync(n => n.UserId == order.UserId);
        Assert.Equal(NotificationType.MenuCancelled, notification.Type);

        _ = periodId;
    }

    [Fact]
    public async Task Cancels_uninvoiced_orders_without_issuing_credit()
    {
        // A napot kizárjuk, de a rendelés még nem volt kiszámlázva — nincs mit jóváírni, különben a
        // dolgozó pénzt kapna egy soha ki nem fizetett napért.
        var (_, orderId) = await SeedActiveOrderAsync(new DateOnly(2026, 8, 20), price: 1400, invoiced: false);

        var result = await sut.Handle(new ExcludeDayCommand(new DateOnly(2026, 8, 20), "Karbantartás", CreatedByUserId: adminId), CancellationToken.None);

        Assert.True(result.IsSuccess);

        await using var db = dbFactory.CreateDbContext();
        var order = await db.MenuOrders.SingleAsync(o => o.Id == orderId);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.False(await db.CreditEntries.AnyAsync(c => c.SourceMenuOrderId == orderId));

        var notification = await db.UserNotifications.SingleAsync(n => n.UserId == order.UserId);
        Assert.Contains("nem volt kiszámlázva", notification.Message);
    }

    [Fact]
    public async Task Rejects_when_the_day_is_already_excluded()
    {
        await SeedActiveOrderAsync(new DateOnly(2026, 8, 20), price: 1400);
        var first = await sut.Handle(new ExcludeDayCommand(new DateOnly(2026, 8, 20), "Első", adminId), CancellationToken.None);
        Assert.True(first.IsSuccess);

        var second = await sut.Handle(new ExcludeDayCommand(new DateOnly(2026, 8, 20), "Második", adminId), CancellationToken.None);

        Assert.False(second.IsSuccess);
        Assert.Equal(ErrorCodes.DayExcluded, second.ErrorCode);
    }

    /// <param name="invoiced">Ha igaz, a rendelés egy már kiállított számlához tartozik — a kizárás
    /// jóváírása csak ilyenkor keletkezik (lásd ICreditService).</param>
    private async Task<(int periodId, int orderId)> SeedActiveOrderAsync(DateOnly date, int price, bool invoiced = true)
    {
        await using var db = dbFactory.CreateDbContext();

        var period = new OrderingPeriod
        {
            Name = "Teszt időszak",
            StartDate = date.AddDays(-10),
            EndDate = date.AddDays(10),
            OrderDeadline = date.AddDays(-15).ToDateTime(new TimeOnly(10, 0)),
        };
        db.OrderingPeriods.Add(period);

        var dish = new MenuDish { Kind = MenuDishKind.Leves, Name = "Teszt menü" };
        db.MenuDishes.Add(dish);
        await db.SaveChangesAsync();

        var dailyMenu = new DailyMenu { Date = date, IsPublished = true };
        dailyMenu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "A", SoupName = "Teszt menü", SoupDishId = dish.Id, SortOrder = 0 });
        db.DailyMenus.Add(dailyMenu);

        var role = new Role { Name = "User" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var user = new User { UserId = 1, UserName = "u1", RoleId = role.Id };
        var admin = new User { UserId = 2, UserName = "admin", RoleId = role.Id };
        db.Users.AddRange(user, admin);
        await db.SaveChangesAsync();
        userId = user.Id;
        adminId = admin.Id;

        int? invoiceId = null;
        if (invoiced)
        {
            var invoice = new PeriodInvoice
            {
                UserId = user.Id,
                OrderingPeriodId = period.Id,
                SequenceNumber = 1,
                GrossHuf = price,
                CreditAppliedHuf = 0,
                PayableHuf = price,
                GeneratedAtUtc = date.AddDays(-14).ToDateTime(new TimeOnly(9, 0)),
            };
            db.PeriodInvoices.Add(invoice);
            await db.SaveChangesAsync();
            invoiceId = invoice.Id;
        }

        var order = new MenuOrder
        {
            UserId = user.Id,
            Date = date,
            OrderingPeriodId = period.Id,
            MenuVariantId = dailyMenu.Variants[0].Id,
            PriceHuf = price,
            Status = OrderStatus.Active,
            PlacedByUserId = user.Id,
            PeriodInvoiceId = invoiceId,
        };
        db.MenuOrders.Add(order);
        await db.SaveChangesAsync();

        return (period.Id, order.Id);
    }
}
