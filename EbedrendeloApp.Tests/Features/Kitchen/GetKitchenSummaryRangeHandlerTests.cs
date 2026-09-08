using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Kitchen.GetKitchenSummaryRange;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Tests.Features.Kitchen;

public class GetKitchenSummaryRangeHandlerTests : IDisposable
{
    private static readonly DateOnly Mon = new(2026, 8, 17);
    private static readonly DateOnly Tue = new(2026, 8, 18);
    private static readonly DateOnly Wed = new(2026, 8, 19);

    private readonly SqliteDbContextFactory dbFactory = new();
    private readonly GetKitchenSummaryRangeHandler sut;

    private int userId;
    private int periodId;
    private readonly Dictionary<(DateOnly, string), int> variantIds = [];

    public GetKitchenSummaryRangeHandlerTests() => sut = new GetKitchenSummaryRangeHandler(dbFactory);

    public void Dispose() => dbFactory.Dispose();

    [Fact]
    public async Task Groups_by_day_and_variant_and_omits_days_with_no_orders()
    {
        await SeedMenuAsync();
        await SeedOrderAsync(Mon, "A");
        await SeedOrderAsync(Mon, "A");
        await SeedOrderAsync(Tue, "B");
        // Wed intentionally has no orders.

        var result = await sut.Handle(new GetKitchenSummaryRangeQuery(Mon, Wed), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal([Mon, Tue], result.Select(d => d.Date).ToList());

        var monday = result.Single(d => d.Date == Mon);
        Assert.Equal(2, monday.TotalPortions);
        Assert.Equal(2, monday.Lines.Single(l => l.VariantCode == "A").Quantity);

        var tuesday = result.Single(d => d.Date == Tue);
        Assert.Equal(1, tuesday.TotalPortions);
        Assert.Equal(1, tuesday.Lines.Single(l => l.VariantCode == "B").Quantity);
    }

    [Fact]
    public async Task IsClosed_is_reported_per_day()
    {
        await SeedMenuAsync();
        await SeedOrderAsync(Mon, "A");
        await SeedOrderAsync(Tue, "A");

        await using (var db = dbFactory.CreateDbContext())
        {
            db.KitchenClosures.Add(new KitchenClosure { Date = Mon, ClosedByUserId = userId, TotalPortions = 1 });
            await db.SaveChangesAsync();
        }

        var result = await sut.Handle(new GetKitchenSummaryRangeQuery(Mon, Wed), CancellationToken.None);

        Assert.True(result.Single(d => d.Date == Mon).IsClosed);
        Assert.False(result.Single(d => d.Date == Tue).IsClosed);
    }

    private async Task SeedMenuAsync()
    {
        await using var db = dbFactory.CreateDbContext();

        var period = new OrderingPeriod
        {
            Name = "Teszt időszak",
            StartDate = Mon.AddDays(-10),
            EndDate = Mon.AddDays(10),
            OrderDeadline = Mon.AddDays(-15).ToDateTime(new TimeOnly(10, 0)),
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

        foreach (var date in new[] { Mon, Tue, Wed })
        {
            var menu = new DailyMenu { Date = date, IsPublished = true };
            menu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "A", SoupName = "Gulyásleves", MainCourseName = "Rántott hús", SoupDishId = soup.Id, SortOrder = 0 });
            menu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "B", SoupName = "Gulyásleves", MainCourseName = "Halászlé", SoupDishId = soup.Id, SortOrder = 1 });
            db.DailyMenus.Add(menu);
            await db.SaveChangesAsync();

            variantIds[(date, "A")] = menu.Variants.Single(v => v.Code == "A").Id;
            variantIds[(date, "B")] = menu.Variants.Single(v => v.Code == "B").Id;
        }
    }

    private int nextWorkerUserId = 100;

    /// <summary>Each call creates a fresh worker — the filtered unique index on (UserId, Date) WHERE
    /// Status = Active (AC 3.1.5) means two Active orders on the same day can't share a user.</summary>
    private async Task SeedOrderAsync(DateOnly date, string variantCode)
    {
        await using var db = dbFactory.CreateDbContext();

        var role = await db.Roles.FirstAsync();
        var worker = new User { UserId = nextWorkerUserId, UserName = $"worker{nextWorkerUserId++}", RoleId = role.Id };
        db.Users.Add(worker);
        await db.SaveChangesAsync();

        db.MenuOrders.Add(new MenuOrder
        {
            UserId = worker.Id,
            Date = date,
            OrderingPeriodId = periodId,
            MenuVariantId = variantIds[(date, variantCode)],
            PriceHuf = 1400,
            Status = OrderStatus.Active,
            PlacedByUserId = worker.Id,
        });
        await db.SaveChangesAsync();
    }
}
