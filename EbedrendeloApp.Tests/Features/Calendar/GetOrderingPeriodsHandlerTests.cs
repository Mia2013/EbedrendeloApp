using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Calendar.GetOrderingPeriod;
using EbedrendeloApp.Features.Calendar.GetOrderingPeriodForDate;
using EbedrendeloApp.Features.Calendar.GetOrderingPeriods;
using EbedrendeloApp.Tests.TestSupport;

namespace EbedrendeloApp.Tests.Features.Calendar;

/// <summary>
/// A három időszak-olvasó use case egy fixture-ön: mindhárom ugyanabból a táblából épít
/// <c>OrderingPeriodDto</c>-t, és mindháromnak ugyanaz a `HasOrders` a kényes pontja — ha az
/// szétcsúszik, az admin felületen egy már megrendelt időszak válna szerkeszthetővé.
/// </summary>
public class GetOrderingPeriodsHandlerTests : IDisposable
{
    private static readonly DateOnly AugustStart = new(2026, 8, 1);
    private static readonly DateOnly AugustEnd = new(2026, 8, 31);
    private static readonly DateOnly SeptemberStart = new(2026, 9, 1);
    private static readonly DateOnly SeptemberEnd = new(2026, 9, 30);

    private readonly SqliteDbContextFactory dbFactory = new();
    private readonly GetOrderingPeriodsHandler listSut;
    private readonly GetOrderingPeriodHandler byIdSut;
    private readonly GetOrderingPeriodForDateHandler byDateSut;

    private int augustId;
    private int septemberId;

    public GetOrderingPeriodsHandlerTests()
    {
        listSut = new GetOrderingPeriodsHandler(dbFactory);
        byIdSut = new GetOrderingPeriodHandler(dbFactory);
        byDateSut = new GetOrderingPeriodForDateHandler(dbFactory);
    }

    public void Dispose() => dbFactory.Dispose();

    [Fact]
    public async Task The_list_is_ordered_by_start_date_and_flags_the_period_that_has_orders()
    {
        await SeedAsync();

        var periods = await listSut.Handle(new GetOrderingPeriodsQuery(), CancellationToken.None);

        Assert.Equal(["Augusztus", "Szeptember"], periods.Select(p => p.Name));
        Assert.True(periods.Single(p => p.Id == augustId).HasOrders);
        Assert.False(periods.Single(p => p.Id == septemberId).HasOrders);
    }

    [Fact]
    public async Task A_period_is_readable_by_id_with_the_same_has_orders_flag()
    {
        await SeedAsync();

        var august = await byIdSut.Handle(new GetOrderingPeriodQuery(augustId), CancellationToken.None);

        Assert.NotNull(august);
        Assert.Equal("Augusztus", august.Name);
        Assert.Equal(AugustStart, august.StartDate);
        Assert.Equal(AugustEnd, august.EndDate);
        Assert.True(august.HasOrders);
    }

    [Fact]
    public async Task An_unknown_id_yields_null_rather_than_an_empty_period()
    {
        await SeedAsync();

        Assert.Null(await byIdSut.Handle(new GetOrderingPeriodQuery(9999), CancellationToken.None));
    }

    [Theory]
    [InlineData(2026, 8, 1)]   // a kezdőnap még bent van
    [InlineData(2026, 8, 15)]
    [InlineData(2026, 8, 31)]  // és a zárónap is
    public async Task The_date_lookup_covers_the_period_inclusively(int year, int month, int day)
    {
        await SeedAsync();

        var period = await byDateSut.Handle(
            new GetOrderingPeriodForDateQuery(new DateOnly(year, month, day)), CancellationToken.None);

        Assert.NotNull(period);
        Assert.Equal(augustId, period.Id);
    }

    [Fact]
    public async Task A_date_no_period_covers_yields_null()
    {
        await SeedAsync();

        Assert.Null(await byDateSut.Handle(
            new GetOrderingPeriodForDateQuery(new DateOnly(2026, 7, 31)), CancellationToken.None));
    }

    private async Task SeedAsync()
    {
        await using var db = dbFactory.CreateDbContext();

        // Szándékosan fordított beszúrási sorrend: így a lista-teszt tényleg a rendezést méri,
        // nem a beszúrás sorrendjét.
        var september = new OrderingPeriod
        {
            Name = "Szeptember",
            StartDate = SeptemberStart,
            EndDate = SeptemberEnd,
            OrderDeadline = SeptemberStart.AddDays(-5).ToDateTime(new TimeOnly(10, 0)),
        };
        var august = new OrderingPeriod
        {
            Name = "Augusztus",
            StartDate = AugustStart,
            EndDate = AugustEnd,
            OrderDeadline = AugustStart.AddDays(-5).ToDateTime(new TimeOnly(10, 0)),
        };
        db.OrderingPeriods.AddRange(september, august);

        var dish = new MenuDish { Kind = MenuDishKind.Leves, Name = "Teszt leves" };
        db.MenuDishes.Add(dish);

        var role = new Role { Name = "User" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var user = new User { UserId = 1, UserName = "u1", VezetekNev = "Teszt", KeresztNev = "Dolgozó", RoleId = role.Id };
        db.Users.Add(user);

        var dailyMenu = new DailyMenu { Date = new DateOnly(2026, 8, 17), IsPublished = true };
        dailyMenu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "A", SoupName = "Teszt leves", SoupDishId = dish.Id, SortOrder = 0 });
        db.DailyMenus.Add(dailyMenu);
        await db.SaveChangesAsync();

        // Csak az augusztusi időszakra van rendelés — a szeptemberi marad szerkeszthető.
        db.MenuOrders.Add(new MenuOrder
        {
            UserId = user.Id,
            Date = new DateOnly(2026, 8, 17),
            OrderingPeriodId = august.Id,
            MenuVariantId = dailyMenu.Variants[0].Id,
            PriceHuf = 1400,
            Status = OrderStatus.Active,
            PlacedByUserId = user.Id,
        });
        await db.SaveChangesAsync();

        augustId = august.Id;
        septemberId = september.Id;
    }
}
