using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.ALaCarte.GetALaCarteMonthlySummary;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Tests.Features.ALaCarte;

public class GetALaCarteMonthlySummaryHandlerTests : IDisposable
{
    private readonly SqliteDbContextFactory dbFactory = new();
    private readonly GetALaCarteMonthlySummaryHandler sut;
    private static readonly DateOnly EarlyInMonth = new(2026, 9, 1);
    private static readonly DateOnly LateInMonth = new(2026, 9, 30);
    private static readonly DateOnly NextMonth = new(2026, 10, 1);
    private static readonly DateOnly PrevMonth = new(2026, 8, 31);

    public GetALaCarteMonthlySummaryHandlerTests() => sut = new GetALaCarteMonthlySummaryHandler(dbFactory);

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
            period = new OrderingPeriod { Name = "P", StartDate = PrevMonth, EndDate = NextMonth, OrderDeadline = DateTime.Now };
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
            // Egy (Category, Name) párhoz a valóságban is egyetlen katalógus-tétel tartozik (unique index) —
            // több felhasználó ugyanazt a tételt ugyanarra a napra az ő aznapi ajánlatán (offer) osztozva
            // rendeli, nem külön-külön tétel/ajánlat párral.
            var item = await db.ALaCarteItems.FirstOrDefaultAsync(i => i.Category == category && i.Name == name);
            if (item is null)
            {
                item = new ALaCarteItem { Name = name, Category = category, PriceHuf = 1000 };
                db.ALaCarteItems.Add(item);
                await db.SaveChangesAsync();
            }

            var offer = await db.ALaCarteDailyOffers.FirstOrDefaultAsync(o => o.Date == date && o.ALaCarteItemId == item.Id);
            if (offer is null)
            {
                offer = new ALaCarteDailyOffer { Date = date, ALaCarteItemId = item.Id, Capacity = 10, OrderedCount = 0 };
                db.ALaCarteDailyOffers.Add(offer);
            }
            offer.OrderedCount++;
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
    public async Task Groups_every_line_in_the_month_regardless_of_day_and_excludes_other_months()
    {
        var (userA, periodId) = await SeedUserAndPeriodAsync(1);
        var (userB, _) = await SeedUserAndPeriodAsync(2);
        var (userC, _) = await SeedUserAndPeriodAsync(3);

        await AddOrderAsync(userA, periodId, EarlyInMonth, (ALaCarteCategory.Foetel, "Rántott szelet"), (ALaCarteCategory.Koret, "Rizi-bizi"));
        await AddOrderAsync(userB, periodId, LateInMonth, (ALaCarteCategory.Foetel, "Rántott szelet"));
        // Outside September — must not be included.
        await AddOrderAsync(userC, periodId, NextMonth, (ALaCarteCategory.Foetel, "Rántott szelet"));

        var result = await sut.Handle(new GetALaCarteMonthlySummaryQuery(2026, 9), CancellationToken.None);

        Assert.Equal(2, result.Lines.Count);
        var szelet = Assert.Single(result.Lines, l => l.ItemName == "Rántott szelet");
        Assert.Equal(2, szelet.Count);
        var riziBizi = Assert.Single(result.Lines, l => l.ItemName == "Rizi-bizi");
        Assert.Equal(1, riziBizi.Count);
    }

    [Fact]
    public async Task Derives_the_soup_portion_count_from_the_months_foetel_lines()
    {
        var (userA, periodId) = await SeedUserAndPeriodAsync(1);
        var (userB, _) = await SeedUserAndPeriodAsync(2);

        await AddOrderAsync(userA, periodId, EarlyInMonth, (ALaCarteCategory.Foetel, "Rántott szelet"), (ALaCarteCategory.Koret, "Rizi-bizi"));
        await AddOrderAsync(userB, periodId, LateInMonth, (ALaCarteCategory.Foetel, "Rántott szelet"));

        var result = await sut.Handle(new GetALaCarteMonthlySummaryQuery(2026, 9), CancellationToken.None);

        Assert.Equal(2, result.SoupPortionCount);
    }

    [Fact]
    public async Task Returns_empty_when_there_are_no_orders_in_the_month()
    {
        var result = await sut.Handle(new GetALaCarteMonthlySummaryQuery(2026, 9), CancellationToken.None);

        Assert.Equal(0, result.SoupPortionCount);
        Assert.Empty(result.Lines);
    }
}
