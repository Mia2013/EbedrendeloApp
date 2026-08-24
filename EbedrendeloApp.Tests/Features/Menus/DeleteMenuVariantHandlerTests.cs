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

        var menu = new DailyMenu { Date = date, IsPublished = true };
        menu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = codeA, Name = $"{codeA} menü", SortOrder = 0 });
        if (codeB is not null)
        {
            menu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = codeB, Name = $"{codeB} menü", SortOrder = 1 });
        }

        db.DailyMenus.Add(menu);
        await db.SaveChangesAsync();

        var variantA = menu.Variants.Single(v => v.Code == codeA);
        var variantB = menu.Variants.SingleOrDefault(v => v.Code == codeB);
        return (variantA.Id, variantB?.Id ?? 0);
    }

    private async Task<int> SeedActiveOrderAsync(DateOnly date, int variantId)
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

        var order = new MenuOrder
        {
            UserId = userId,
            Date = date,
            OrderingPeriodId = period.Id,
            MenuVariantId = variantId,
            PriceHuf = 1400,
            Status = OrderStatus.Active,
            PlacedByUserId = userId,
        };
        db.MenuOrders.Add(order);
        await db.SaveChangesAsync();
        return order.Id;
    }
}
