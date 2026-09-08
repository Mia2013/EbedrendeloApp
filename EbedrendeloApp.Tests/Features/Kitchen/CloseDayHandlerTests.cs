using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Kitchen.CloseDay;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Tests.Features.Kitchen;

public class CloseDayHandlerTests : IDisposable
{
    private static readonly DateOnly Thu = new(2026, 8, 20);

    private readonly SqliteDbContextFactory dbFactory = new();
    private readonly FixedAppClock clock = new(new DateTime(2026, 8, 20, 15, 0, 0));
    private readonly CloseDayHandler sut;

    private int adminId;
    private int variantAId;
    private int variantBId;

    public CloseDayHandlerTests() => sut = new CloseDayHandler(dbFactory, clock);

    public void Dispose() => dbFactory.Dispose();

    [Fact]
    public async Task Snapshots_active_orders_by_variant_and_ignores_cancelled_ones()
    {
        await SeedMenuAsync();
        await SeedOrderAsync(variantAId, OrderStatus.Active);
        await SeedOrderAsync(variantAId, OrderStatus.Active);
        await SeedOrderAsync(variantBId, OrderStatus.Active);
        await SeedOrderAsync(variantAId, OrderStatus.Cancelled);

        var result = await sut.Handle(new CloseDayCommand(Thu, adminId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.TotalPortions);
        Assert.Equal(2, result.Value.Lines.Count);
        Assert.Equal(2, result.Value.Lines.Single(l => l.VariantCode == "A").Quantity);
        Assert.Equal(1, result.Value.Lines.Single(l => l.VariantCode == "B").Quantity);

        await using var db = dbFactory.CreateDbContext();
        var closure = await db.KitchenClosures.Include(k => k.Lines).SingleAsync(k => k.Date == Thu);
        Assert.Equal(adminId, closure.ClosedByUserId);
        Assert.Equal(clock.UtcNow.UtcDateTime, closure.ClosedAtUtc);
        Assert.Equal(3, closure.TotalPortions);
        Assert.Equal(2, closure.Lines.Count);
    }

    [Fact]
    public async Task Closing_a_day_with_no_orders_succeeds_with_an_empty_snapshot()
    {
        await SeedMenuAsync();

        var result = await sut.Handle(new CloseDayCommand(Thu, adminId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.TotalPortions);
        Assert.Empty(result.Value.Lines);
    }

    [Fact]
    public async Task Rejects_a_day_that_is_already_closed()
    {
        await SeedMenuAsync();
        var first = await sut.Handle(new CloseDayCommand(Thu, adminId), CancellationToken.None);
        Assert.True(first.IsSuccess);

        var second = await sut.Handle(new CloseDayCommand(Thu, adminId), CancellationToken.None);

        Assert.False(second.IsSuccess);
        Assert.Equal(ErrorCodes.DayClosed, second.ErrorCode);
    }

    [Fact]
    public async Task Reclosing_a_reopened_day_inserts_a_new_closure_row_and_keeps_the_original()
    {
        await SeedMenuAsync();
        var first = await sut.Handle(new CloseDayCommand(Thu, adminId), CancellationToken.None);
        Assert.True(first.IsSuccess);

        await using (var db = dbFactory.CreateDbContext())
        {
            var firstClosure = await db.KitchenClosures.SingleAsync(k => k.Date == Thu);
            db.KitchenClosureReopenings.Add(new KitchenClosureReopening
            {
                KitchenClosureId = firstClosure.Id,
                ReopenedAtUtc = clock.UtcNow.UtcDateTime,
                ReopenedByUserId = adminId,
            });
            await db.SaveChangesAsync();
        }

        await SeedOrderAsync(variantAId, OrderStatus.Active);

        var second = await sut.Handle(new CloseDayCommand(Thu, adminId), CancellationToken.None);

        Assert.True(second.IsSuccess);
        Assert.Equal(1, second.Value!.TotalPortions);

        await using var verifyDb = dbFactory.CreateDbContext();
        var closures = await verifyDb.KitchenClosures.Where(k => k.Date == Thu).ToListAsync();
        Assert.Equal(2, closures.Count);
    }

    private async Task SeedMenuAsync()
    {
        await using var db = dbFactory.CreateDbContext();

        var period = new OrderingPeriod
        {
            Name = "Teszt időszak",
            StartDate = Thu.AddDays(-10),
            EndDate = Thu.AddDays(10),
            OrderDeadline = Thu.AddDays(-15).ToDateTime(new TimeOnly(10, 0)),
        };
        db.OrderingPeriods.Add(period);

        var role = new Role { Name = "User" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var admin = new User { UserId = 2, UserName = "admin", RoleId = role.Id };
        db.Users.Add(admin);
        await db.SaveChangesAsync();
        adminId = admin.Id;

        var soup = new MenuDish { Kind = MenuDishKind.Leves, Name = "Gulyásleves" };
        db.MenuDishes.Add(soup);
        await db.SaveChangesAsync();

        var menu = new DailyMenu { Date = Thu, IsPublished = true };
        menu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "A", SoupName = "Gulyásleves", MainCourseName = "Rántott hús", SoupDishId = soup.Id, SortOrder = 0 });
        menu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "B", SoupName = "Gulyásleves", MainCourseName = "Halászlé", SoupDishId = soup.Id, SortOrder = 1 });
        db.DailyMenus.Add(menu);
        await db.SaveChangesAsync();

        variantAId = menu.Variants.Single(v => v.Code == "A").Id;
        variantBId = menu.Variants.Single(v => v.Code == "B").Id;

        this.periodId = period.Id;
    }

    private int periodId;
    private int nextWorkerUserId = 100;

    /// <summary>Each call creates a fresh worker — the filtered unique index on (UserId, Date) WHERE
    /// Status = Active (AC 3.1.5) means two Active orders on the same day can't share a user.</summary>
    private async Task SeedOrderAsync(int variantId, OrderStatus status)
    {
        await using var db = dbFactory.CreateDbContext();

        var role = await db.Roles.FirstAsync();
        var worker = new User { UserId = nextWorkerUserId, UserName = $"worker{nextWorkerUserId++}", RoleId = role.Id };
        db.Users.Add(worker);
        await db.SaveChangesAsync();

        db.MenuOrders.Add(new MenuOrder
        {
            UserId = worker.Id,
            Date = Thu,
            OrderingPeriodId = periodId,
            MenuVariantId = variantId,
            PriceHuf = 1400,
            Status = status,
            PlacedByUserId = worker.Id,
        });
        await db.SaveChangesAsync();
    }
}
