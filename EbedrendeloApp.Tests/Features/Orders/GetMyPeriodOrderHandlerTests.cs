using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Orders.GetMyPeriodOrder;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Tests.Features.Orders;

public class GetMyPeriodOrderHandlerTests : IDisposable
{
    private static readonly DateOnly Mon = new(2026, 8, 17);
    private static readonly DateOnly Tue = new(2026, 8, 18);
    private static readonly DateOnly Wed = new(2026, 8, 19);

    private readonly SqliteDbContextFactory dbFactory = new();
    private readonly GetMyPeriodOrderHandler sut;

    private int periodId;
    private int ownerId;
    private int placerId;

    public GetMyPeriodOrderHandlerTests()
    {
        sut = new GetMyPeriodOrderHandler(dbFactory);
    }

    public void Dispose() => dbFactory.Dispose();

    private async Task SeedAsync()
    {
        await using var db = dbFactory.CreateDbContext();

        var period = new OrderingPeriod
        {
            Name = "Teszt időszak",
            StartDate = Mon,
            EndDate = new DateOnly(2026, 8, 21),
            OrderDeadline = new DateTime(2026, 8, 1, 10, 0, 0),
        };
        db.OrderingPeriods.Add(period);

        var role = new Role { Name = "User" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var owner = new User { UserId = 1, UserName = "u1", VezetekNev = "Kovács", KeresztNev = "János", RoleId = role.Id };
        var placer = new User { UserId = 2, UserName = "u2", VezetekNev = "Nagy", KeresztNev = "Anna", RoleId = role.Id };
        db.Users.AddRange(owner, placer);
        await db.SaveChangesAsync();

        periodId = period.Id;
        ownerId = owner.Id;
        placerId = placer.Id;

        var dishNames = new[] { "Gulyásleves", "Húsleves", "Rántott szelet", "Halászlé" };
        var dishes = dishNames.Select(n => new MenuDish { Kind = MenuDishKind.Leves, Name = n }).ToList();
        db.MenuDishes.AddRange(dishes);
        await db.SaveChangesAsync();
        var dishIdByName = dishes.ToDictionary(d => d.Name, d => d.Id);

        var monMenu = new DailyMenu { Date = Mon, IsPublished = true };
        monMenu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "A", SoupName = "Gulyásleves", SoupDishId = dishIdByName["Gulyásleves"], MainCourseName = "Rántott hús", SortOrder = 0 });
        monMenu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "B", SoupName = "Húsleves", SoupDishId = dishIdByName["Húsleves"], SortOrder = 1 });
        db.DailyMenus.Add(monMenu);

        var tueMenu = new DailyMenu { Date = Tue, IsPublished = true };
        tueMenu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "A", SoupName = "Rántott szelet", SoupDishId = dishIdByName["Rántott szelet"], SortOrder = 0 });
        db.DailyMenus.Add(tueMenu);

        var wedMenu = new DailyMenu { Date = Wed, IsPublished = true };
        wedMenu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "A", SoupName = "Halászlé", SoupDishId = dishIdByName["Halászlé"], SortOrder = 0 });
        db.DailyMenus.Add(wedMenu);

        await db.SaveChangesAsync();

        // Own active order, placed by the owner — no "placed by someone else" display name expected.
        db.MenuOrders.Add(new MenuOrder
        {
            UserId = ownerId,
            Date = Mon,
            OrderingPeriodId = periodId,
            MenuVariantId = monMenu.Variants[0].Id,
            PriceHuf = 1400,
            Status = OrderStatus.Active,
            PlacedByUserId = ownerId,
        });

        // Cancelled order, placed by someone else — the placer's name should surface (AC 3.3.3).
        db.MenuOrders.Add(new MenuOrder
        {
            UserId = ownerId,
            Date = Tue,
            OrderingPeriodId = periodId,
            MenuVariantId = tueMenu.Variants[0].Id,
            PriceHuf = 1400,
            Status = OrderStatus.Cancelled,
            PlacedByUserId = placerId,
            CancellationReason = CancellationReason.ByUser,
            CancelledAtUtc = new DateTime(2026, 8, 15, 10, 0, 0),
            CancelledByUserId = ownerId,
        });

        // Reassigned order — ReassignedFromVariantCode must surface (AC 3.3.4).
        db.MenuOrders.Add(new MenuOrder
        {
            UserId = ownerId,
            Date = Wed,
            OrderingPeriodId = periodId,
            MenuVariantId = wedMenu.Variants[0].Id,
            PriceHuf = 1400,
            Status = OrderStatus.Active,
            PlacedByUserId = ownerId,
            ReassignedFromVariantCode = "B",
            ReassignedAtUtc = new DateTime(2026, 8, 16, 9, 0, 0),
        });

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Returns_active_and_cancelled_orders_together_ordered_by_date()
    {
        await SeedAsync();

        var result = await sut.Handle(new GetMyPeriodOrderQuery(ownerId, periodId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var rows = result.Value!;
        Assert.Equal(3, rows.Count);
        Assert.Equal([Mon, Tue, Wed], rows.Select(r => r.Date));
    }

    [Fact]
    public async Task Placed_by_display_name_is_only_set_when_someone_else_placed_the_order()
    {
        await SeedAsync();

        var result = await sut.Handle(new GetMyPeriodOrderQuery(ownerId, periodId), CancellationToken.None);

        var rows = result.Value!.ToDictionary(r => r.Date);
        Assert.Null(rows[Mon].PlacedByDisplayName);
        Assert.Equal("Nagy Anna", rows[Tue].PlacedByDisplayName);
    }

    [Fact]
    public async Task Cancelled_order_surfaces_its_cancellation_reason()
    {
        await SeedAsync();

        var result = await sut.Handle(new GetMyPeriodOrderQuery(ownerId, periodId), CancellationToken.None);

        var cancelled = result.Value!.Single(r => r.Date == Tue);
        Assert.Equal(OrderStatus.Cancelled, cancelled.Status);
        Assert.Equal(CancellationReason.ByUser, cancelled.CancellationReason);
        Assert.NotNull(cancelled.CancelledAtUtc);
    }

    [Fact]
    public async Task Reassigned_order_surfaces_the_original_variant_code()
    {
        await SeedAsync();

        var result = await sut.Handle(new GetMyPeriodOrderQuery(ownerId, periodId), CancellationToken.None);

        var reassigned = result.Value!.Single(r => r.Date == Wed);
        Assert.Equal("B", reassigned.ReassignedFromVariantCode);
    }

    [Fact]
    public async Task VariantName_combines_soup_and_main_course_when_the_variant_has_one()
    {
        await SeedAsync();

        var result = await sut.Handle(new GetMyPeriodOrderQuery(ownerId, periodId), CancellationToken.None);

        var mon = result.Value!.Single(r => r.Date == Mon);
        Assert.Equal("Gulyásleves + Rántott hús", mon.VariantName);
    }

    [Fact]
    public async Task VariantName_falls_back_to_the_soup_name_when_the_variant_has_no_main_course()
    {
        await SeedAsync();

        var result = await sut.Handle(new GetMyPeriodOrderQuery(ownerId, periodId), CancellationToken.None);

        var wed = result.Value!.Single(r => r.Date == Wed);
        Assert.Equal("Halászlé", wed.VariantName);
    }

    [Fact]
    public async Task Returns_NotFound_for_an_unknown_period()
    {
        await SeedAsync();

        var result = await sut.Handle(new GetMyPeriodOrderQuery(ownerId, periodId + 999), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NotFound, result.ErrorCode);
    }
}
