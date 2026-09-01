using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.ALaCarte.GetALaCarteDailySummary;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Tests.Features.ALaCarte;

public class GetALaCarteDailySummaryHandlerTests : IDisposable
{
    private readonly SqliteDbContextFactory dbFactory = new();
    private readonly GetALaCarteDailySummaryHandler sut;
    private static readonly DateOnly Today = new(2026, 9, 1);
    private static readonly DateOnly Tomorrow = new(2026, 9, 2);

    public GetALaCarteDailySummaryHandlerTests() => sut = new GetALaCarteDailySummaryHandler(dbFactory);

    public void Dispose() => dbFactory.Dispose();

    private async Task<(int userId, int periodId)> SeedUserAndPeriodAsync(int userNumber)
    {
        await using var db = dbFactory.CreateDbContext();
        var role = await db.Roles.FirstOrDefaultAsync() ?? new Role { Name = "User" };
        if (role.Id == 0)
        {
            db.Roles.Add(role);
            await db.SaveChangesAsync();
        }
        var period = await db.OrderingPeriods.FirstOrDefaultAsync();
        if (period is null)
        {
            period = new OrderingPeriod { Name = "P", StartDate = Today, EndDate = Tomorrow, OrderDeadline = DateTime.Now };
            db.OrderingPeriods.Add(period);
        }
        var user = new User { UserId = userNumber, UserName = $"u{userNumber}", RoleId = role.Id };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (user.Id, period.Id);
    }

    private async Task AddOrderAsync(int userId, int periodId, DateOnly date, params (ALaCarteCategory Category, string Name)[] items)
    {
        await using var db = dbFactory.CreateDbContext();
        var order = new ALaCarteOrder { UserId = userId, Date = date, OrderingPeriodId = periodId, PlacedByUserId = userId, TotalHuf = 0 };
        foreach (var (category, name) in items)
        {
            var item = new ALaCarteItem { Name = name, Category = category, PriceHuf = 1000 };
            db.ALaCarteItems.Add(item);
            await db.SaveChangesAsync();
            var offer = new ALaCarteDailyOffer { Date = date, ALaCarteItemId = item.Id, Capacity = 10, OrderedCount = 1 };
            db.ALaCarteDailyOffers.Add(offer);
            await db.SaveChangesAsync();

            order.Lines.Add(new ALaCarteOrderLine
            {
                ALaCarteOrderId = 0,
                ALaCarteDailyOfferId = offer.Id,
                ItemNameSnapshot = name,
                CategorySnapshot = category,
                UnitPriceHuf = 1000,
            });
        }
        db.ALaCarteOrders.Add(order);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Groups_todays_lines_by_category_and_name_and_derives_the_soup_portion_count()
    {
        var (userA, periodId) = await SeedUserAndPeriodAsync(1);
        var (userB, _) = await SeedUserAndPeriodAsync(2);

        await AddOrderAsync(userA, periodId, Today,
            (ALaCarteCategory.Foetel, "Rántott szelet"), (ALaCarteCategory.Koret, "Rizi-bizi"));
        await AddOrderAsync(userB, periodId, Today,
            (ALaCarteCategory.Foetel, "Rántott szelet"), (ALaCarteCategory.Desszert, "Palacsinta"));

        // Different day — must not be included.
        var (userC, _) = await SeedUserAndPeriodAsync(3);
        await AddOrderAsync(userC, periodId, Tomorrow, (ALaCarteCategory.Foetel, "Rántott szelet"));

        var result = await sut.Handle(new GetALaCarteDailySummaryQuery(Today), CancellationToken.None);

        Assert.Equal(2, result.SoupPortionCount); // 2 Főétel lines today
        Assert.Equal(3, result.Lines.Count);
        var szelet = Assert.Single(result.Lines, l => l.ItemName == "Rántott szelet");
        Assert.Equal(2, szelet.Count);
    }

    [Fact]
    public async Task Returns_zero_counts_when_there_are_no_orders_for_the_date()
    {
        var result = await sut.Handle(new GetALaCarteDailySummaryQuery(Today), CancellationToken.None);

        Assert.Equal(0, result.SoupPortionCount);
        Assert.Empty(result.Lines);
    }
}
