using EbedrendeloApp.Common.Calendar;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Menus.GetTodayMenuForUser;
using EbedrendeloApp.Tests.TestSupport;

namespace EbedrendeloApp.Tests.Features.Menus;

public class GetTodayMenuForUserHandlerTests : IDisposable
{
    // 2026-08-17 is a Monday.
    private readonly SqliteDbContextFactory dbFactory = new();
    private readonly FixedAppClock clock = new(new DateTime(2026, 8, 17, 9, 0, 0));
    private readonly GetTodayMenuForUserHandler sut;

    private int userId;

    public GetTodayMenuForUserHandlerTests()
    {
        sut = new GetTodayMenuForUserHandler(dbFactory, clock, new WorkingDayCalculator());
    }

    public void Dispose() => dbFactory.Dispose();

    [Fact]
    public async Task Reports_not_orderable_on_a_weekend()
    {
        var weekendClock = new FixedAppClock(new DateTime(2026, 8, 15, 9, 0, 0)); // Saturday
        var handler = new GetTodayMenuForUserHandler(dbFactory, weekendClock, new WorkingDayCalculator());

        var result = await handler.Handle(new GetTodayMenuForUserQuery(1), CancellationToken.None);

        Assert.False(result.IsOrderableToday);
        Assert.Equal(ErrorCodes.NotWorkingDay, result.NotOrderableReason);
    }

    [Fact]
    public async Task Reports_not_orderable_when_today_is_excluded()
    {
        await SeedUserAsync();
        await using (var db = dbFactory.CreateDbContext())
        {
            db.ExcludedDays.Add(new ExcludedDay { Date = clock.Today, Reason = "Karbantartás", CreatedByUserId = userId });
            await db.SaveChangesAsync();
        }

        var result = await sut.Handle(new GetTodayMenuForUserQuery(userId), CancellationToken.None);

        Assert.False(result.IsOrderableToday);
        Assert.Equal(ErrorCodes.DayExcluded, result.NotOrderableReason);
    }

    [Fact]
    public async Task Reports_not_orderable_when_there_is_no_published_menu()
    {
        var result = await sut.Handle(new GetTodayMenuForUserQuery(1), CancellationToken.None);

        Assert.False(result.IsOrderableToday);
        Assert.Equal(ErrorCodes.MenuNotPublished, result.NotOrderableReason);
    }

    [Fact]
    public async Task Returns_variants_with_no_selection_when_the_user_has_not_ordered()
    {
        await SeedUserAsync();
        await SeedPublishedMenuAsync();

        var result = await sut.Handle(new GetTodayMenuForUserQuery(userId), CancellationToken.None);

        Assert.True(result.IsOrderableToday);
        Assert.Null(result.NotOrderableReason);
        Assert.Equal(2, result.Variants.Count);
        Assert.Null(result.MySelection);
    }

    [Fact]
    public async Task Returns_the_users_own_active_selection()
    {
        await SeedUserAsync();
        var (_, variantAId) = await SeedPublishedMenuAsync();
        await SeedActiveOrderAsync(variantAId);

        var result = await sut.Handle(new GetTodayMenuForUserQuery(userId), CancellationToken.None);

        Assert.NotNull(result.MySelection);
        Assert.Equal("A", result.MySelection!.VariantCode);
        Assert.Equal(1400, result.MySelection.PriceHuf);
    }

    [Fact]
    public async Task Includes_ala_carte_offers_with_free_count()
    {
        await SeedUserAsync();
        await SeedPublishedMenuAsync();
        await using (var db = dbFactory.CreateDbContext())
        {
            var item = new ALaCarteItem { Name = "Húsleves", Category = ALaCarteCategory.Leves, PriceHuf = 650, IsActive = true };
            db.ALaCarteItems.Add(item);
            await db.SaveChangesAsync();

            db.ALaCarteDailyOffers.Add(new ALaCarteDailyOffer { Date = clock.Today, ALaCarteItemId = item.Id, Capacity = 10, OrderedCount = 3 });
            await db.SaveChangesAsync();
        }

        var result = await sut.Handle(new GetTodayMenuForUserQuery(userId), CancellationToken.None);

        var offer = Assert.Single(result.ALaCarteOffers);
        Assert.Equal("Húsleves", offer.Name);
        Assert.Equal(7, offer.FreeCount);
    }

    private async Task SeedUserAsync()
    {
        await using var db = dbFactory.CreateDbContext();
        var role = new Role { Name = "User" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var user = new User { UserId = 1, UserName = "u1", RoleId = role.Id };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        userId = user.Id;
    }

    private async Task<(int menuId, int variantAId)> SeedPublishedMenuAsync()
    {
        await using var db = dbFactory.CreateDbContext();
        var menu = new DailyMenu { Date = clock.Today, IsPublished = true };
        menu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "A", Name = "A menü", SortOrder = 0 });
        menu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "B", Name = "B menü", SortOrder = 1 });
        db.DailyMenus.Add(menu);
        await db.SaveChangesAsync();
        return (menu.Id, menu.Variants[0].Id);
    }

    private async Task SeedActiveOrderAsync(int variantId)
    {
        await using var db = dbFactory.CreateDbContext();
        var period = new OrderingPeriod
        {
            Name = "Teszt időszak",
            StartDate = clock.Today.AddDays(-10),
            EndDate = clock.Today.AddDays(10),
            OrderDeadline = clock.Today.AddDays(-15).ToDateTime(new TimeOnly(10, 0)),
        };
        db.OrderingPeriods.Add(period);
        await db.SaveChangesAsync();

        db.MenuOrders.Add(new MenuOrder
        {
            UserId = userId,
            Date = clock.Today,
            OrderingPeriodId = period.Id,
            MenuVariantId = variantId,
            PriceHuf = 1400,
            Status = OrderStatus.Active,
            PlacedByUserId = userId,
        });
        await db.SaveChangesAsync();
    }
}
