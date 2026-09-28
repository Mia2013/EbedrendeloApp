using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Kitchen.GetKitchenSummary;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Tests.Features.Kitchen;

public class GetKitchenSummaryHandlerTests : IDisposable
{
    private static readonly DateOnly Thu = new(2026, 8, 20);

    private readonly SqliteDbContextFactory dbFactory = new();
    private readonly GetKitchenSummaryHandler sut;

    private int userId;
    private int periodId;
    private int variantAId;
    private int variantBId;

    public GetKitchenSummaryHandlerTests() => sut = new GetKitchenSummaryHandler(dbFactory);

    public void Dispose() => dbFactory.Dispose();

    [Fact]
    public async Task Groups_active_orders_by_variant_and_ignores_cancelled_ones()
    {
        await SeedMenuAsync();
        await SeedOrderAsync(variantAId, OrderStatus.Active);
        await SeedOrderAsync(variantAId, OrderStatus.Active);
        await SeedOrderAsync(variantBId, OrderStatus.Active);
        await SeedOrderAsync(variantBId, OrderStatus.Cancelled);

        var result = await sut.Handle(new GetKitchenSummaryQuery(Thu), CancellationToken.None);

        Assert.Equal(3, result.TotalPortions);
        Assert.Equal(2, result.Lines.Single(l => l.VariantCode == "A").Quantity);
        Assert.Equal(1, result.Lines.Single(l => l.VariantCode == "B").Quantity);
    }

    [Fact]
    public async Task A_variant_nobody_ordered_is_listed_with_zero_rather_than_dropped()
    {
        // AC 6.1.1 „variánsonként (A/B/C)": a konyhának látnia kell, hogy a B menüre nem rendelt
        // senki — a hiányzó sorból ez nem derül ki.
        await SeedMenuAsync();
        await SeedOrderAsync(variantAId, OrderStatus.Active);

        var result = await sut.Handle(new GetKitchenSummaryQuery(Thu), CancellationToken.None);

        Assert.Equal(["A", "B"], result.Lines.Select(l => l.VariantCode));
        Assert.Equal(1, result.Lines.Single(l => l.VariantCode == "A").Quantity);
        Assert.Equal(0, result.Lines.Single(l => l.VariantCode == "B").Quantity);
        Assert.Equal(1, result.TotalPortions);
    }

    [Fact]
    public async Task A_day_with_no_orders_lists_every_variant_with_zero()
    {
        await SeedMenuAsync();

        var result = await sut.Handle(new GetKitchenSummaryQuery(Thu), CancellationToken.None);

        Assert.Equal(["A", "B"], result.Lines.Select(l => l.VariantCode));
        Assert.All(result.Lines, l => Assert.Equal(0, l.Quantity));
        Assert.Equal(0, result.TotalPortions);
        Assert.False(result.IsClosed);
    }

    [Fact]
    public async Task A_day_without_a_published_menu_has_nothing_to_list()
    {
        await SeedMenuAsync(published: false);

        var result = await sut.Handle(new GetKitchenSummaryQuery(Thu), CancellationToken.None);

        Assert.Empty(result.Lines);
        Assert.Equal(0, result.TotalPortions);
    }

    [Fact]
    public async Task An_order_on_a_since_removed_variant_still_has_to_be_cooked()
    {
        await SeedMenuAsync();
        await SeedOrderAsync(variantBId, OrderStatus.Active);
        await RemoveVariantAsync(variantBId);

        var result = await sut.Handle(new GetKitchenSummaryQuery(Thu), CancellationToken.None);

        // A törölt variáns kiesik az élő menüből, de a rá leadott adag nem tűnhet el az összesítőből.
        Assert.Equal(1, result.Lines.Single(l => l.VariantCode == "B").Quantity);
        Assert.Equal(0, result.Lines.Single(l => l.VariantCode == "A").Quantity);
        Assert.Equal(1, result.TotalPortions);
    }

    [Fact]
    public async Task IsClosed_reflects_an_active_kitchen_closure_and_clears_after_reopening()
    {
        await SeedMenuAsync();

        await using (var db = dbFactory.CreateDbContext())
        {
            var closure = new KitchenClosure { Date = Thu, ClosedByUserId = userId, TotalPortions = 0 };
            db.KitchenClosures.Add(closure);
            await db.SaveChangesAsync();
        }

        var whileClosed = await sut.Handle(new GetKitchenSummaryQuery(Thu), CancellationToken.None);
        Assert.True(whileClosed.IsClosed);

        await using (var db = dbFactory.CreateDbContext())
        {
            var closure = await db.KitchenClosures.SingleAsync(k => k.Date == Thu);
            db.KitchenClosureReopenings.Add(new KitchenClosureReopening { KitchenClosureId = closure.Id, ReopenedByUserId = userId });
            await db.SaveChangesAsync();
        }

        var afterReopen = await sut.Handle(new GetKitchenSummaryQuery(Thu), CancellationToken.None);
        Assert.False(afterReopen.IsClosed);
    }

    private async Task SeedMenuAsync(bool published = true)
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

        var user = new User { UserId = 1, UserName = "u1", RoleId = role.Id };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        userId = user.Id;
        periodId = period.Id;

        var soup = new MenuDish { Kind = MenuDishKind.Leves, Name = "Gulyásleves" };
        db.MenuDishes.Add(soup);
        await db.SaveChangesAsync();

        var menu = new DailyMenu { Date = Thu, IsPublished = published };
        menu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "A", SoupName = "Gulyásleves", MainCourseName = "Rántott hús", SoupDishId = soup.Id, SortOrder = 0 });
        menu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "B", SoupName = "Gulyásleves", MainCourseName = "Halászlé", SoupDishId = soup.Id, SortOrder = 1 });
        db.DailyMenus.Add(menu);
        await db.SaveChangesAsync();

        variantAId = menu.Variants.Single(v => v.Code == "A").Id;
        variantBId = menu.Variants.Single(v => v.Code == "B").Id;
    }

    private async Task RemoveVariantAsync(int variantId)
    {
        await using var db = dbFactory.CreateDbContext();
        var variant = await db.MenuVariants.SingleAsync(v => v.Id == variantId);
        variant.RemovedAtUtc = new DateTime(2026, 8, 19, 10, 0, 0, DateTimeKind.Utc);
        await db.SaveChangesAsync();
    }

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
