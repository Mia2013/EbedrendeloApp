using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Services;
using EbedrendeloApp.Common.Time;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Menus.DeleteDailyMenu;
using EbedrendeloApp.Features.Menus.UpsertDailyMenu;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Tests.Features.Menus;

public class DeleteDailyMenuHandlerTests : IDisposable
{
    private readonly SqliteDbContextFactory dbFactory = new();
    private readonly FixedAppClock clock = new(new DateTime(2026, 8, 17, 9, 0, 0));
    private readonly DeleteDailyMenuHandler sut;

    private int userId;
    private int adminId;

    public DeleteDailyMenuHandlerTests()
    {
        sut = new DeleteDailyMenuHandler(dbFactory, clock, new CreditService(), new NotificationService());
    }

    public void Dispose() => dbFactory.Dispose();

    [Fact]
    public async Task Rejects_when_the_day_is_already_closed()
    {
        var date = new DateOnly(2026, 8, 20);
        await SeedMenuOnlyAsync(date);
        await using (var db = dbFactory.CreateDbContext())
        {
            db.KitchenClosures.Add(new KitchenClosure { Date = date, ClosedByUserId = adminId, TotalPortions = 0 });
            await db.SaveChangesAsync();
        }

        var result = await sut.Handle(new DeleteDailyMenuCommand(date, adminId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.DayClosed, result.ErrorCode);
    }

    [Fact]
    public async Task Rejects_when_there_is_no_menu_for_the_day()
    {
        var result = await sut.Handle(new DeleteDailyMenuCommand(new DateOnly(2026, 8, 20), 1), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Cancels_and_credits_all_active_orders_with_MenuDeleted_reason()
    {
        var date = new DateOnly(2026, 8, 20);
        var orderId = await SeedMenuWithActiveOrderAsync(date);

        var result = await sut.Handle(new DeleteDailyMenuCommand(date, adminId), CancellationToken.None);

        Assert.True(result.IsSuccess);

        await using var db = dbFactory.CreateDbContext();
        var order = await db.MenuOrders.SingleAsync(o => o.Id == orderId);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal(CancellationReason.MenuDeleted, order.CancellationReason);

        var credit = await db.CreditEntries.SingleAsync(c => c.SourceMenuOrderId == orderId);
        Assert.Equal(1400, credit.AmountHuf);

        var notification = await db.UserNotifications.SingleAsync(n => n.RelatedMenuOrderId == orderId);
        Assert.Equal(NotificationType.MenuCancelled, notification.Type);
    }

    [Fact]
    public async Task Soft_deletes_menu_and_variants_leaving_the_date_unpublished()
    {
        var date = new DateOnly(2026, 8, 20);
        var menuId = await SeedMenuOnlyAsync(date);

        var result = await sut.Handle(new DeleteDailyMenuCommand(date, adminId), CancellationToken.None);

        Assert.True(result.IsSuccess);

        await using var db = dbFactory.CreateDbContext();
        var menu = await db.DailyMenus.Include(m => m.Variants).SingleAsync(m => m.Id == menuId);
        Assert.NotNull(menu.RemovedAtUtc);
        Assert.False(menu.IsPublished);
        Assert.All(menu.Variants, v => Assert.NotNull(v.RemovedAtUtc));
    }

    [Fact]
    public async Task Upsert_can_recreate_the_menu_on_the_same_date_after_delete()
    {
        var date = new DateOnly(2026, 8, 20);
        await SeedMenuOnlyAsync(date);
        await sut.Handle(new DeleteDailyMenuCommand(date, adminId), CancellationToken.None);

        var upsertHandler = new UpsertDailyMenuHandler(
            dbFactory, clock, new MenuReassignmentService(new CreditService(), new NotificationService()), new NotificationService());

        int soupDishId;
        await using (var db = dbFactory.CreateDbContext())
        {
            var dish = new MenuDish { Kind = MenuDishKind.Leves, Name = "Új menü" };
            db.MenuDishes.Add(dish);
            await db.SaveChangesAsync();
            soupDishId = dish.Id;
        }

        var result = await upsertHandler.Handle(
            new UpsertDailyMenuCommand(date, null, [new MenuVariantInput("A", soupDishId, null, 0)], adminId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    private async Task<int> SeedMenuOnlyAsync(DateOnly date)
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

        var dish = new MenuDish { Kind = MenuDishKind.Leves, Name = "A menü" };
        db.MenuDishes.Add(dish);
        await db.SaveChangesAsync();

        var menu = new DailyMenu { Date = date, IsPublished = true };
        menu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "A", SoupName = "A menü", SoupDishId = dish.Id, SortOrder = 0 });
        db.DailyMenus.Add(menu);
        await db.SaveChangesAsync();
        return menu.Id;
    }

    private async Task<int> SeedMenuWithActiveOrderAsync(DateOnly date)
    {
        var menuId = await SeedMenuOnlyAsync(date);

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

        var variant = await db.MenuVariants.SingleAsync(v => v.DailyMenuId == menuId);

        var order = new MenuOrder
        {
            UserId = userId,
            Date = date,
            OrderingPeriodId = period.Id,
            MenuVariantId = variant.Id,
            PriceHuf = 1400,
            Status = OrderStatus.Active,
            PlacedByUserId = userId,
        };
        db.MenuOrders.Add(order);
        await db.SaveChangesAsync();
        return order.Id;
    }
}
