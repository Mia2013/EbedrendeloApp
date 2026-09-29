using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Services;
using EbedrendeloApp.Common.Time;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Menus.DeleteMenuVariant;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Tests.Features.Menus;

public class DeleteMenuVariantHandlerTests : IDisposable
{
    private readonly SqliteDbContextFactory dbFactory = new();
    private readonly FixedAppClock clock = new(new DateTime(2026, 8, 17, 9, 0, 0));
    private readonly DeleteMenuVariantHandler sut;

    private int userId;
    private int adminId;

    public DeleteMenuVariantHandlerTests()
    {
        sut = new DeleteMenuVariantHandler(dbFactory, clock, new MenuReassignmentService(new CreditService(), new NotificationService()));
    }

    public void Dispose() => dbFactory.Dispose();

    [Fact]
    public async Task Rejects_a_past_date()
    {
        var result = await sut.Handle(new DeleteMenuVariantCommand(new DateOnly(2026, 8, 16), "A", 1), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NotFutureDate, result.ErrorCode);
    }

    [Fact]
    public async Task Rejects_when_the_day_is_already_closed()
    {
        var date = new DateOnly(2026, 8, 20);
        await SeedMenuAsync(date, "A");
        await using (var db = dbFactory.CreateDbContext())
        {
            db.KitchenClosures.Add(new KitchenClosure { Date = date, ClosedByUserId = adminId, TotalPortions = 0 });
            await db.SaveChangesAsync();
        }

        var result = await sut.Handle(new DeleteMenuVariantCommand(date, "A", adminId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.DayClosed, result.ErrorCode);
    }

    [Fact]
    public async Task Rejects_when_there_is_no_menu_for_the_day()
    {
        var result = await sut.Handle(new DeleteMenuVariantCommand(new DateOnly(2026, 8, 20), "A", 1), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Rejects_when_the_variant_code_does_not_exist_on_the_day()
    {
        var date = new DateOnly(2026, 8, 20);
        await SeedMenuAsync(date, "A");

        var result = await sut.Handle(new DeleteMenuVariantCommand(date, "Z", 1), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Reassigns_active_orders_to_the_remaining_variant_and_soft_deletes_the_row()
    {
        var date = new DateOnly(2026, 8, 20);
        var (variantAId, variantBId) = await SeedMenuAsync(date, "A", "B");
        var orderId = await SeedActiveOrderAsync(date, variantAId);

        var result = await sut.Handle(new DeleteMenuVariantCommand(date, "A", adminId), CancellationToken.None);

        Assert.True(result.IsSuccess);

        await using var db = dbFactory.CreateDbContext();
        var order = await db.MenuOrders.SingleAsync(o => o.Id == orderId);
        Assert.Equal(OrderStatus.Active, order.Status);
        Assert.Equal(variantBId, order.MenuVariantId);
        Assert.Equal("A", order.ReassignedFromVariantCode);

        var variantA = await db.MenuVariants.SingleAsync(v => v.Id == variantAId);
        Assert.NotNull(variantA.RemovedAtUtc);
    }

    [Fact]
    public async Task Cancels_and_credits_active_orders_when_no_other_variant_remains()
    {
        var date = new DateOnly(2026, 8, 20);
        var (variantAId, _) = await SeedMenuAsync(date, "A");
        var orderId = await SeedActiveOrderAsync(date, variantAId);

        var result = await sut.Handle(new DeleteMenuVariantCommand(date, "A", adminId), CancellationToken.None);

        Assert.True(result.IsSuccess);

        await using var db = dbFactory.CreateDbContext();
        var order = await db.MenuOrders.SingleAsync(o => o.Id == orderId);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal(CancellationReason.VariantRemoved, order.CancellationReason);

        var notification = await db.UserNotifications.SingleAsync(n => n.RelatedMenuOrderId == orderId);
        Assert.Equal(NotificationType.MenuCancelled, notification.Type);

        var menu = await db.DailyMenus.SingleAsync(m => m.Date == date);
        Assert.False(menu.IsPublished);
    }

    [Fact]
    public async Task Cancelling_an_invoiced_order_notifies_the_owner_with_CreditIssued_and_the_placer_with_MenuCancelled()
    {
        // 01 §3.3 + AC 8.1.3 — kiszámlázott napnál jóváírás keletkezik: a tulajdonos CreditIssued-ot kap,
        // a leadó (akit a jóváírás nem illet) MenuCancelled-et.
        var date = new DateOnly(2026, 8, 20);
        var (variantAId, _) = await SeedMenuAsync(date, "A");
        var orderId = await SeedActiveOrderAsync(date, variantAId, invoiced: true);
        var colleagueId = await ReassignPlacerToColleagueAsync(orderId);

        await sut.Handle(new DeleteMenuVariantCommand(date, "A", adminId), CancellationToken.None);

        await using var db = dbFactory.CreateDbContext();
        Assert.True(await db.CreditEntries.AnyAsync(c => c.SourceMenuOrderId == orderId));

        var ownerNotification = await db.UserNotifications.SingleAsync(n => n.UserId == userId);
        Assert.Equal(NotificationType.CreditIssued, ownerNotification.Type);

        var placerNotification = await db.UserNotifications.SingleAsync(n => n.UserId == colleagueId);
        Assert.Equal(NotificationType.MenuCancelled, placerNotification.Type);
        Assert.Equal("Az általad leadott rendelés lemondásra került", placerNotification.Title);
    }

    [Fact]
    public async Task Reassigning_an_order_placed_by_a_colleague_notifies_the_placer_too()
    {
        // AC 8.1.3 — az átvezetésről a leadó is tud.
        var date = new DateOnly(2026, 8, 20);
        var (variantAId, _) = await SeedMenuAsync(date, "A", "B");
        var orderId = await SeedActiveOrderAsync(date, variantAId);
        var colleagueId = await ReassignPlacerToColleagueAsync(orderId);

        await sut.Handle(new DeleteMenuVariantCommand(date, "A", adminId), CancellationToken.None);

        await using var db = dbFactory.CreateDbContext();
        var ownerNotification = await db.UserNotifications.SingleAsync(n => n.UserId == userId);
        Assert.Equal(NotificationType.OrderReassigned, ownerNotification.Type);

        var placerNotification = await db.UserNotifications.SingleAsync(n => n.UserId == colleagueId);
        Assert.Equal(NotificationType.OrderReassigned, placerNotification.Type);
        Assert.Equal("Az általad leadott rendelés átvezetésre került", placerNotification.Title);
    }

    [Fact]
    public async Task Unpublishes_the_day_when_the_last_variant_is_deleted_even_without_active_orders()
    {
        // Regression test: without this, deleting the only variant left a "published" DailyMenu with an
        // empty Variants list — GetOrderableDaysHandler/GetTodayMenuForUserHandler/GetPeriodMenuHandler
        // would then report the day as orderable/having a menu with nothing to actually order.
        var date = new DateOnly(2026, 8, 20);
        await SeedMenuAsync(date, "A");

        var result = await sut.Handle(new DeleteMenuVariantCommand(date, "A", adminId), CancellationToken.None);

        Assert.True(result.IsSuccess);

        await using var db = dbFactory.CreateDbContext();
        var menu = await db.DailyMenus.SingleAsync(m => m.Date == date);
        Assert.False(menu.IsPublished);
    }

    [Fact]
    public async Task Keeps_the_day_published_when_another_variant_still_remains()
    {
        var date = new DateOnly(2026, 8, 20);
        await SeedMenuAsync(date, "A", "B");

        var result = await sut.Handle(new DeleteMenuVariantCommand(date, "A", adminId), CancellationToken.None);

        Assert.True(result.IsSuccess);

        await using var db = dbFactory.CreateDbContext();
        var menu = await db.DailyMenus.SingleAsync(m => m.Date == date);
        Assert.True(menu.IsPublished);
    }

    private async Task<(int variantAId, int variantBId)> SeedMenuAsync(DateOnly date, string codeA, string? codeB = null)
    {
        await using var db = dbFactory.CreateDbContext();

        var role = new Role { Name = "User" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var user = new User { UserId = 1, UserName = "u1", RoleId = role.Id };
        var admin = new User { UserId = 2, UserName = "admin", RoleId = role.Id };
        db.Users.AddRange(user, admin);
        await db.SaveChangesAsync();
        userId = user.Id;
        adminId = admin.Id;

        var dishA = new MenuDish { Kind = MenuDishKind.Leves, Name = $"{codeA} menü" };
        db.MenuDishes.Add(dishA);
        MenuDish? dishB = null;
        if (codeB is not null)
        {
            dishB = new MenuDish { Kind = MenuDishKind.Leves, Name = $"{codeB} menü" };
            db.MenuDishes.Add(dishB);
        }

        await db.SaveChangesAsync();

        var menu = new DailyMenu { Date = date, IsPublished = true };
        menu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = codeA, SoupName = $"{codeA} menü", SoupDishId = dishA.Id, SortOrder = 0 });
        if (codeB is not null)
        {
            menu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = codeB, SoupName = $"{codeB} menü", SoupDishId = dishB!.Id, SortOrder = 1 });
        }

        db.DailyMenus.Add(menu);
        await db.SaveChangesAsync();

        var variantA = menu.Variants.Single(v => v.Code == codeA);
        var variantB = menu.Variants.SingleOrDefault(v => v.Code == codeB);
        return (variantA.Id, variantB?.Id ?? 0);
    }

    private async Task<int> ReassignPlacerToColleagueAsync(int orderId)
    {
        await using var db = dbFactory.CreateDbContext();
        var colleague = new User { UserId = 3, UserName = "kollega", RoleId = db.Roles.First().Id };
        db.Users.Add(colleague);
        await db.SaveChangesAsync();

        var order = await db.MenuOrders.SingleAsync(o => o.Id == orderId);
        order.PlacedByUserId = colleague.Id;
        await db.SaveChangesAsync();
        return colleague.Id;
    }

    /// <param name="invoiced">Ha igaz, a rendelés egy már kiállított számlához tartozik — lemondáskor
    /// jóváírás csak ilyenkor keletkezik (lásd ICreditService).</param>
    private async Task<int> SeedActiveOrderAsync(DateOnly date, int variantId, bool invoiced = false)
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
        await db.SaveChangesAsync();

        int? invoiceId = null;
        if (invoiced)
        {
            var invoice = new PeriodInvoice
            {
                UserId = userId,
                OrderingPeriodId = period.Id,
                SequenceNumber = 1,
                GrossHuf = 1400,
                CreditAppliedHuf = 0,
                PayableHuf = 1400,
                GeneratedAtUtc = date.AddDays(-14).ToDateTime(new TimeOnly(9, 0)),
            };
            db.PeriodInvoices.Add(invoice);
            await db.SaveChangesAsync();
            invoiceId = invoice.Id;
        }

        var order = new MenuOrder
        {
            UserId = userId,
            Date = date,
            OrderingPeriodId = period.Id,
            MenuVariantId = variantId,
            PriceHuf = 1400,
            Status = OrderStatus.Active,
            PlacedByUserId = userId,
            PeriodInvoiceId = invoiceId,
        };
        db.MenuOrders.Add(order);
        await db.SaveChangesAsync();
        return order.Id;
    }
}
