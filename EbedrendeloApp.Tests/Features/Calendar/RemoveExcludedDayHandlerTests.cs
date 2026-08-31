using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Services;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Calendar.ExcludeDay;
using EbedrendeloApp.Features.Calendar.RemoveExcludedDay;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Tests.Features.Calendar;

public class RemoveExcludedDayHandlerTests : IDisposable
{
    private static readonly DateOnly ExcludedDate = new(2026, 8, 20);

    private readonly SqliteDbContextFactory dbFactory = new();
    private readonly FixedAppClock clock = new(new DateTime(2026, 8, 17, 9, 0, 0));
    private readonly RemoveExcludedDayHandler sut;

    public RemoveExcludedDayHandlerTests()
    {
        var creditService = new CreditService();
        var notificationService = new NotificationService();
        sut = new RemoveExcludedDayHandler(dbFactory, clock, creditService, notificationService);
        excludeHandler = new ExcludeDayHandler(dbFactory, clock, creditService, notificationService);
    }

    private readonly ExcludeDayHandler excludeHandler;
    private int adminId;

    public void Dispose() => dbFactory.Dispose();

    [Fact]
    public async Task Rejects_todays_date()
    {
        var result = await sut.Handle(new RemoveExcludedDayCommand(clock.Today, true, 1), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NotFutureDate, result.ErrorCode);
    }

    [Fact]
    public async Task Restores_the_order_when_its_credit_is_untouched_and_no_invoice_exists()
    {
        var (periodId, orderId, userId) = await SeedExcludedOrderAsync();

        var result = await sut.Handle(new RemoveExcludedDayCommand(ExcludedDate, true, PerformedByUserId: adminId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.RestoredCount);
        Assert.Equal(0, result.Value.SkippedCount);

        await using var db = dbFactory.CreateDbContext();

        Assert.False(await db.ExcludedDays.AnyAsync(e => e.Date == ExcludedDate));

        var order = await db.MenuOrders.SingleAsync(o => o.Id == orderId);
        Assert.Equal(OrderStatus.Active, order.Status);
        Assert.Null(order.CancellationReason);
        Assert.Null(order.CancelledByExcludedDayId);

        var revoked = await db.CreditEntries.SingleAsync(c => c.Kind == CreditEntryKind.CreditRevoked);
        Assert.Equal(-1400, revoked.AmountHuf);

        var originalCredit = await db.CreditEntries.SingleAsync(c => c.SourceMenuOrderId == orderId);
        Assert.Equal(0, originalCredit.RemainingHuf);

        Assert.Contains(await db.UserNotifications.Where(n => n.UserId == userId).ToListAsync(), n => n.Type == NotificationType.OrderRestored);

        _ = periodId;
    }

    [Fact]
    public async Task Skips_restoring_the_order_when_a_period_invoice_already_exists()
    {
        var (periodId, orderId, userId) = await SeedExcludedOrderAsync();

        await using (var db = dbFactory.CreateDbContext())
        {
            db.PeriodInvoices.Add(new PeriodInvoice
            {
                UserId = userId,
                OrderingPeriodId = periodId,
                MenuGrossHuf = 1400,
                ALaCarteGrossHuf = 0,
                GrossHuf = 1400,
                CreditAppliedHuf = 0,
                MenuPayableHuf = 1400,
                ALaCartePayableHuf = 0,
                PayableHuf = 1400,
            });
            await db.SaveChangesAsync();
        }

        var result = await sut.Handle(new RemoveExcludedDayCommand(ExcludedDate, true, PerformedByUserId: adminId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.RestoredCount);
        Assert.Equal(1, result.Value.SkippedCount);

        await using var verifyDb = dbFactory.CreateDbContext();
        var order = await verifyDb.MenuOrders.SingleAsync(o => o.Id == orderId);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public async Task Skips_restoring_the_order_when_its_credit_was_already_partially_consumed()
    {
        var (_, orderId, _) = await SeedExcludedOrderAsync();

        await using (var db = dbFactory.CreateDbContext())
        {
            var credit = await db.CreditEntries.SingleAsync(c => c.SourceMenuOrderId == orderId);
            credit.RemainingHuf = 700; // half spent elsewhere — no longer equals AmountHuf (1400)
            await db.SaveChangesAsync();
        }

        var result = await sut.Handle(new RemoveExcludedDayCommand(ExcludedDate, true, PerformedByUserId: adminId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.RestoredCount);
        Assert.Equal(1, result.Value.SkippedCount);

        await using var verifyDb = dbFactory.CreateDbContext();
        var order = await verifyDb.MenuOrders.SingleAsync(o => o.Id == orderId);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public async Task Skips_restoring_the_order_when_a_newer_active_order_exists_for_the_same_day()
    {
        var (periodId, orderId, userId) = await SeedExcludedOrderAsync();

        await using (var db = dbFactory.CreateDbContext())
        {
            var order = await db.MenuOrders.SingleAsync(o => o.Id == orderId);
            var variantId = order.MenuVariantId;

            db.MenuOrders.Add(new MenuOrder
            {
                UserId = userId,
                Date = ExcludedDate,
                OrderingPeriodId = periodId,
                MenuVariantId = variantId,
                PriceHuf = 1400,
                Status = OrderStatus.Active,
                PlacedByUserId = userId,
            });
            await db.SaveChangesAsync();
        }

        var result = await sut.Handle(new RemoveExcludedDayCommand(ExcludedDate, true, PerformedByUserId: adminId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.RestoredCount);
        Assert.Equal(1, result.Value.SkippedCount);

        await using var verifyDb = dbFactory.CreateDbContext();
        var originalOrder = await verifyDb.MenuOrders.SingleAsync(o => o.Id == orderId);
        Assert.Equal(OrderStatus.Cancelled, originalOrder.Status);
    }

    [Fact]
    public async Task Restores_correctly_after_a_second_exclude_restore_cycle_on_the_same_order()
    {
        // Regression test: ExcludeDayHandler always inserts a brand-new CreditEntry per cycle (never
        // reuses one), so after exclude -> restore -> exclude -> restore, TWO CancellationCredit rows
        // exist for the same order (the first one already zeroed by the first restore). Picking "the
        // oldest" instead of "the still-live one" would find the stale, already-zeroed entry and wrongly
        // skip the second restore as "credit already consumed elsewhere".
        var (_, orderId, _) = await SeedExcludedOrderAsync();

        var firstRestore = await sut.Handle(new RemoveExcludedDayCommand(ExcludedDate, true, PerformedByUserId: adminId), CancellationToken.None);
        Assert.True(firstRestore.IsSuccess);
        Assert.Equal(1, firstRestore.Value!.RestoredCount);
        Assert.Equal(0, firstRestore.Value.SkippedCount);

        var reExclude = await excludeHandler.Handle(new ExcludeDayCommand(ExcludedDate, "Ismételt karbantartás", adminId), CancellationToken.None);
        Assert.True(reExclude.IsSuccess);

        await using (var db = dbFactory.CreateDbContext())
        {
            var cancellationCredits = await db.CreditEntries
                .Where(c => c.SourceMenuOrderId == orderId && c.Kind == CreditEntryKind.CancellationCredit)
                .ToListAsync();
            Assert.Equal(2, cancellationCredits.Count);
            Assert.Single(cancellationCredits, c => c.RemainingHuf == c.AmountHuf); // the live, current-cycle entry
        }

        var secondRestore = await sut.Handle(new RemoveExcludedDayCommand(ExcludedDate, true, PerformedByUserId: adminId), CancellationToken.None);

        Assert.True(secondRestore.IsSuccess);
        Assert.Equal(1, secondRestore.Value!.RestoredCount);
        Assert.Equal(0, secondRestore.Value.SkippedCount);

        await using var verifyDb = dbFactory.CreateDbContext();
        var order = await verifyDb.MenuOrders.SingleAsync(o => o.Id == orderId);
        Assert.Equal(OrderStatus.Active, order.Status);
        Assert.Null(order.CancellationReason);
    }

    [Fact]
    public async Task Does_not_touch_orders_when_restore_flag_is_false()
    {
        var (_, orderId, _) = await SeedExcludedOrderAsync();

        var result = await sut.Handle(new RemoveExcludedDayCommand(ExcludedDate, false, PerformedByUserId: adminId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.RestoredCount);
        Assert.Equal(0, result.Value.SkippedCount);

        await using var db = dbFactory.CreateDbContext();
        var order = await db.MenuOrders.SingleAsync(o => o.Id == orderId);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.False(await db.ExcludedDays.AnyAsync(e => e.Date == ExcludedDate));
    }

    private async Task<(int periodId, int orderId, int userId)> SeedExcludedOrderAsync()
    {
        int periodId, orderId, userId;

        await using (var db = dbFactory.CreateDbContext())
        {
            var period = new OrderingPeriod
            {
                Name = "Teszt időszak",
                StartDate = ExcludedDate.AddDays(-10),
                EndDate = ExcludedDate.AddDays(10),
                OrderDeadline = ExcludedDate.AddDays(-15).ToDateTime(new TimeOnly(10, 0)),
            };
            db.OrderingPeriods.Add(period);

            var dish = new MenuDish { Kind = MenuDishKind.Leves, Name = "Teszt menü" };
            db.MenuDishes.Add(dish);
            await db.SaveChangesAsync();

            var dailyMenu = new DailyMenu { Date = ExcludedDate, IsPublished = true };
            dailyMenu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "A", SoupName = "Teszt menü", SoupDishId = dish.Id, SortOrder = 0 });
            db.DailyMenus.Add(dailyMenu);

            var role = new Role { Name = "User" };
            db.Roles.Add(role);
            await db.SaveChangesAsync();

            var user = new User { UserId = 1, UserName = "u1", RoleId = role.Id };
            var admin = new User { UserId = 2, UserName = "admin", RoleId = role.Id };
            db.Users.AddRange(user, admin);
            await db.SaveChangesAsync();
            adminId = admin.Id;

            var order = new MenuOrder
            {
                UserId = user.Id,
                Date = ExcludedDate,
                OrderingPeriodId = period.Id,
                MenuVariantId = dailyMenu.Variants[0].Id,
                PriceHuf = 1400,
                Status = OrderStatus.Active,
                PlacedByUserId = user.Id,
            };
            db.MenuOrders.Add(order);
            await db.SaveChangesAsync();

            periodId = period.Id;
            orderId = order.Id;
            userId = user.Id;
        }

        var excludeResult = await excludeHandler.Handle(new ExcludeDayCommand(ExcludedDate, "Karbantartás", adminId), CancellationToken.None);
        Assert.True(excludeResult.IsSuccess);

        return (periodId, orderId, userId);
    }
}
