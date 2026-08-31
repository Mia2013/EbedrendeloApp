using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Orders.GetUserOrders;
using EbedrendeloApp.Tests.TestSupport;

namespace EbedrendeloApp.Tests.Features.Orders;

public class GetUserOrdersHandlerTests : IDisposable
{
    private static readonly DateOnly Mon = new(2026, 8, 17);
    private static readonly DateOnly Tue = new(2026, 8, 18);

    private readonly SqliteDbContextFactory dbFactory = new();
    private readonly GetUserOrdersHandler sut;

    private int period1Id;
    private int period2Id;
    private int user1Id;
    private int user2Id;
    private int adminId;

    public GetUserOrdersHandlerTests()
    {
        sut = new GetUserOrdersHandler(dbFactory);
    }

    public void Dispose() => dbFactory.Dispose();

    private async Task SeedAsync()
    {
        await using var db = dbFactory.CreateDbContext();

        var period1 = new OrderingPeriod { Name = "1. időszak", StartDate = Mon, EndDate = new DateOnly(2026, 8, 21), OrderDeadline = new DateTime(2026, 8, 1, 10, 0, 0) };
        var period2 = new OrderingPeriod { Name = "2. időszak", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 9, 30), OrderDeadline = new DateTime(2026, 8, 20, 10, 0, 0) };
        db.OrderingPeriods.AddRange(period1, period2);

        var role = new Role { Name = "User" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var user1 = new User { UserId = 1, UserName = "u1", VezetekNev = "Kovács", KeresztNev = "János", RoleId = role.Id };
        var user2 = new User { UserId = 2, UserName = "u2", VezetekNev = "Nagy", KeresztNev = "Anna", RoleId = role.Id };
        var admin = new User { UserId = 3, UserName = "admin", VezetekNev = "Rendszer", KeresztNev = "Admin", RoleId = role.Id };
        db.Users.AddRange(user1, user2, admin);
        await db.SaveChangesAsync();

        period1Id = period1.Id;
        period2Id = period2.Id;
        user1Id = user1.Id;
        user2Id = user2.Id;
        adminId = admin.Id;

        var dishNames = new[] { "Gulyásleves", "Halászlé" };
        var dishes = dishNames.Select(n => new MenuDish { Kind = MenuDishKind.Leves, Name = n }).ToList();
        db.MenuDishes.AddRange(dishes);
        await db.SaveChangesAsync();
        var dishIdByName = dishes.ToDictionary(d => d.Name, d => d.Id);

        var monMenu = new DailyMenu { Date = Mon, IsPublished = true };
        monMenu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "A", SoupName = "Gulyásleves", SoupDishId = dishIdByName["Gulyásleves"], SortOrder = 0 });
        db.DailyMenus.Add(monMenu);

        var septMenu = new DailyMenu { Date = new DateOnly(2026, 9, 1), IsPublished = true };
        septMenu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "A", SoupName = "Halászlé", SoupDishId = dishIdByName["Halászlé"], MainCourseName = "Fogas fehérboros mártásban", SortOrder = 0 });
        db.DailyMenus.Add(septMenu);
        await db.SaveChangesAsync();

        // o1: period1, user1, Active
        db.MenuOrders.Add(new MenuOrder
        {
            UserId = user1Id,
            Date = Mon,
            OrderingPeriodId = period1Id,
            MenuVariantId = monMenu.Variants[0].Id,
            PriceHuf = 1400,
            Status = OrderStatus.Active,
            PlacedByUserId = user1Id,
        });

        // o2: period1, user2, Cancelled by admin (DayExcluded)
        db.MenuOrders.Add(new MenuOrder
        {
            UserId = user2Id,
            Date = Tue,
            OrderingPeriodId = period1Id,
            MenuVariantId = monMenu.Variants[0].Id,
            PriceHuf = 1400,
            Status = OrderStatus.Cancelled,
            PlacedByUserId = user2Id,
            CancellationReason = CancellationReason.DayExcluded,
            CancelledAtUtc = new DateTime(2026, 8, 16, 8, 0, 0),
            CancelledByUserId = adminId,
        });

        // o3: period2, user1, Active
        db.MenuOrders.Add(new MenuOrder
        {
            UserId = user1Id,
            Date = new DateOnly(2026, 9, 1),
            OrderingPeriodId = period2Id,
            MenuVariantId = septMenu.Variants[0].Id,
            PriceHuf = 1400,
            Status = OrderStatus.Active,
            PlacedByUserId = user1Id,
        });

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Filters_by_ordering_period()
    {
        await SeedAsync();

        var result = await sut.Handle(new GetUserOrdersQuery(period1Id, null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal([Mon, Tue], result.Value!.Select(o => o.Date).OrderBy(d => d));
    }

    [Fact]
    public async Task Filters_by_user()
    {
        await SeedAsync();

        var result = await sut.Handle(new GetUserOrdersQuery(null, user1Id, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
        Assert.All(result.Value, o => Assert.Equal(user1Id, o.UserId));
    }

    [Fact]
    public async Task Filters_by_status()
    {
        await SeedAsync();

        var result = await sut.Handle(new GetUserOrdersQuery(null, null, OrderStatus.Cancelled), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var order = Assert.Single(result.Value!);
        Assert.Equal(user2Id, order.UserId);
    }

    [Fact]
    public async Task Combines_filters()
    {
        await SeedAsync();

        var result = await sut.Handle(new GetUserOrdersQuery(period1Id, user1Id, OrderStatus.Active), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var order = Assert.Single(result.Value!);
        Assert.Equal(Mon, order.Date);
    }

    [Fact]
    public async Task VariantName_combines_soup_and_main_course_when_the_variant_has_one()
    {
        await SeedAsync();

        var result = await sut.Handle(new GetUserOrdersQuery(period2Id, user1Id, null), CancellationToken.None);

        var order = Assert.Single(result.Value!);
        Assert.Equal("Halászlé + Fogas fehérboros mártásban", order.VariantName);
    }

    [Fact]
    public async Task VariantName_falls_back_to_the_soup_name_when_the_variant_has_no_main_course()
    {
        await SeedAsync();

        var result = await sut.Handle(new GetUserOrdersQuery(period1Id, user1Id, null), CancellationToken.None);

        var order = Assert.Single(result.Value!);
        Assert.Equal("Gulyásleves", order.VariantName);
    }

    [Fact]
    public async Task Populates_audit_fields_for_a_cancelled_order()
    {
        await SeedAsync();

        var result = await sut.Handle(new GetUserOrdersQuery(period1Id, user2Id, null), CancellationToken.None);

        var order = Assert.Single(result.Value!);
        Assert.Equal("Nagy Anna", order.UserDisplayName);
        Assert.Equal("Nagy Anna", order.PlacedByDisplayName);
        Assert.Equal(adminId, order.CancelledByUserId);
        Assert.Equal("Rendszer Admin", order.CancelledByDisplayName);
        Assert.Equal(CancellationReason.DayExcluded, order.CancellationReason);
    }
}
