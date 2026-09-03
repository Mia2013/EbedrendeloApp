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

    /// <summary>A leves sosem önálló rendelési sor (AC 4.2.8) — a valóságban egy Főétel-rendelés
    /// <see cref="ALaCarteOrderLine.IncludesSoup"/>=true jelöléssel "hordozza" az aznap felkínált levest.</summary>
    private async Task AddFoetelOrderWithSoupAsync(int userId, int periodId, DateOnly date, string foetelName, string soupName)
    {
        await using var db = dbFactory.CreateDbContext();

        var soupItem = await db.ALaCarteItems.FirstOrDefaultAsync(i => i.Category == ALaCarteCategory.Leves && i.Name == soupName);
        if (soupItem is null)
        {
            soupItem = new ALaCarteItem { Name = soupName, Category = ALaCarteCategory.Leves, PriceHuf = 300 };
            db.ALaCarteItems.Add(soupItem);
            await db.SaveChangesAsync();
        }
        if (!await db.ALaCarteDailyOffers.AnyAsync(o => o.Date == date && o.ALaCarteItemId == soupItem.Id))
        {
            // A valóságban a SetDailyOfferHandler a Leves kapacitást mindig int.MaxValue-ra kényszeríti
            // (nincs keret) — itt is ezt tükrözzük, hogy a Capacity > 0 szűrés (aktív tétel a hónapban)
            // a levest is helyesen kínáltnak lássa.
            db.ALaCarteDailyOffers.Add(new ALaCarteDailyOffer { Date = date, ALaCarteItemId = soupItem.Id, Capacity = int.MaxValue, OrderedCount = 0 });
            await db.SaveChangesAsync();
        }

        var foetelItem = await db.ALaCarteItems.FirstOrDefaultAsync(i => i.Category == ALaCarteCategory.Foetel && i.Name == foetelName);
        if (foetelItem is null)
        {
            foetelItem = new ALaCarteItem { Name = foetelName, Category = ALaCarteCategory.Foetel, PriceHuf = 1000 };
            db.ALaCarteItems.Add(foetelItem);
            await db.SaveChangesAsync();
        }
        var foetelOffer = await db.ALaCarteDailyOffers.FirstOrDefaultAsync(o => o.Date == date && o.ALaCarteItemId == foetelItem.Id);
        if (foetelOffer is null)
        {
            foetelOffer = new ALaCarteDailyOffer { Date = date, ALaCarteItemId = foetelItem.Id, Capacity = 10, OrderedCount = 0 };
            db.ALaCarteDailyOffers.Add(foetelOffer);
            await db.SaveChangesAsync();
        }
        foetelOffer.OrderedCount++;
        await db.SaveChangesAsync();

        var order = new ALaCarteOrder { UserId = userId, Date = date, OrderingPeriodId = periodId, PlacedByUserId = userId, TotalHuf = 0 };
        order.Lines.Add(new ALaCarteOrderLine
        {
            ALaCarteOrderId = 0,
            ALaCarteDailyOfferId = foetelOffer.Id,
            ItemNameSnapshot = foetelName,
            CategorySnapshot = ALaCarteCategory.Foetel,
            UnitPriceHuf = 1300,
            IncludesSoup = true,
        });
        db.ALaCarteOrders.Add(order);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Adds_a_synthetic_soup_line_per_day_from_the_offered_soup_and_the_days_includesSoup_count()
    {
        var (userA, periodId) = await SeedUserAndPeriodAsync(1);
        var (userB, _) = await SeedUserAndPeriodAsync(2);

        await AddFoetelOrderWithSoupAsync(userA, periodId, EarlyInMonth, "Rántott szelet", "Csontleves");
        await AddFoetelOrderWithSoupAsync(userB, periodId, EarlyInMonth, "Bakonyi szelet", "Csontleves");

        var result = await sut.Handle(new GetALaCarteMonthlySummaryQuery(2026, 9), CancellationToken.None);

        var soupLine = Assert.Single(result.Lines, l => l.Category == ALaCarteCategory.Leves);
        Assert.Equal(EarlyInMonth, soupLine.Date);
        Assert.Equal("Csontleves", soupLine.ItemName);
        Assert.Equal(2, soupLine.Count);
    }

    [Fact]
    public async Task Does_not_throw_when_two_soups_are_offered_on_the_same_day()
    {
        var (userA, periodId) = await SeedUserAndPeriodAsync(1);
        await AddFoetelOrderWithSoupAsync(userA, periodId, EarlyInMonth, "Rántott szelet", "Zöldségleves");

        // Adatanomália: egy második, különböző leves-tétel aktív ajánlata ugyanarra a napra
        // (a valóságban a SetDailyOfferHandler ezt elutasítaná — lásd AC 4.1.4 — de a DB-szinten
        // nincs erre constraint, lásd 01-szerver-architektura.md 13. ismert korlátozás).
        await using (var db = dbFactory.CreateDbContext())
        {
            var secondSoup = new ALaCarteItem { Name = "Almaleves", Category = ALaCarteCategory.Leves, PriceHuf = 300 };
            db.ALaCarteItems.Add(secondSoup);
            await db.SaveChangesAsync();
            db.ALaCarteDailyOffers.Add(new ALaCarteDailyOffer { Date = EarlyInMonth, ALaCarteItemId = secondSoup.Id, Capacity = int.MaxValue, OrderedCount = 0 });
            await db.SaveChangesAsync();
        }

        var result = await sut.Handle(new GetALaCarteMonthlySummaryQuery(2026, 9), CancellationToken.None);

        var soupLine = Assert.Single(result.Lines, l => l.Category == ALaCarteCategory.Leves);
        Assert.Equal("Almaleves", soupLine.ItemName); // "Almaleves" < "Zöldségleves" ordinálisan
    }

    [Fact]
    public async Task Groups_lines_per_day_and_excludes_other_months()
    {
        var (userA, periodId) = await SeedUserAndPeriodAsync(1);
        var (userB, _) = await SeedUserAndPeriodAsync(2);
        var (userC, _) = await SeedUserAndPeriodAsync(3);

        await AddOrderAsync(userA, periodId, EarlyInMonth, (ALaCarteCategory.Foetel, "Rántott szelet"), (ALaCarteCategory.Koret, "Rizi-bizi"));
        await AddOrderAsync(userB, periodId, LateInMonth, (ALaCarteCategory.Foetel, "Rántott szelet"));
        // Outside September — must not be included.
        await AddOrderAsync(userC, periodId, NextMonth, (ALaCarteCategory.Foetel, "Rántott szelet"));

        var result = await sut.Handle(new GetALaCarteMonthlySummaryQuery(2026, 9), CancellationToken.None);

        // Same item on two different days stays two separate day-rows, not merged into one monthly total —
        // the kitchen list needs a per-day breakdown, not just a month-wide sum.
        Assert.Equal(3, result.Lines.Count);
        var szeletEarly = Assert.Single(result.Lines, l => l.ItemName == "Rántott szelet" && l.Date == EarlyInMonth);
        Assert.Equal(1, szeletEarly.Count);
        var szeletLate = Assert.Single(result.Lines, l => l.ItemName == "Rántott szelet" && l.Date == LateInMonth);
        Assert.Equal(1, szeletLate.Count);
        var riziBizi = Assert.Single(result.Lines, l => l.ItemName == "Rizi-bizi");
        Assert.Equal(EarlyInMonth, riziBizi.Date);
        Assert.Equal(1, riziBizi.Count);
    }

    [Fact]
    public async Task Sums_multiple_orders_of_the_same_item_on_the_same_day_into_one_line()
    {
        var (userA, periodId) = await SeedUserAndPeriodAsync(1);
        var (userB, _) = await SeedUserAndPeriodAsync(2);

        await AddOrderAsync(userA, periodId, EarlyInMonth, (ALaCarteCategory.Foetel, "Rántott szelet"));
        await AddOrderAsync(userB, periodId, EarlyInMonth, (ALaCarteCategory.Foetel, "Rántott szelet"));

        var result = await sut.Handle(new GetALaCarteMonthlySummaryQuery(2026, 9), CancellationToken.None);

        var szelet = Assert.Single(result.Lines);
        Assert.Equal(EarlyInMonth, szelet.Date);
        Assert.Equal(2, szelet.Count);
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

    [Fact]
    public async Task An_item_offered_all_month_but_never_ordered_still_gets_an_offered_item_entry()
    {
        await using var db = dbFactory.CreateDbContext();
        var item = new ALaCarteItem { Name = "Sosem rendelt köret", Category = ALaCarteCategory.Koret, PriceHuf = 500 };
        db.ALaCarteItems.Add(item);
        await db.SaveChangesAsync();
        db.ALaCarteDailyOffers.Add(new ALaCarteDailyOffer { Date = EarlyInMonth, ALaCarteItemId = item.Id, Capacity = 10, OrderedCount = 0 });
        await db.SaveChangesAsync();

        var result = await sut.Handle(new GetALaCarteMonthlySummaryQuery(2026, 9), CancellationToken.None);

        Assert.Empty(result.Lines);
        var offeredItem = Assert.Single(result.OfferedItems);
        Assert.Equal(ALaCarteCategory.Koret, offeredItem.Category);
        Assert.Equal("Sosem rendelt köret", offeredItem.ItemName);
    }

    [Fact]
    public async Task An_item_with_zero_capacity_that_day_is_not_offered_and_gets_no_column()
    {
        await using var db = dbFactory.CreateDbContext();
        var item = new ALaCarteItem { Name = "Kivezetett köret", Category = ALaCarteCategory.Koret, PriceHuf = 500 };
        db.ALaCarteItems.Add(item);
        await db.SaveChangesAsync();
        db.ALaCarteDailyOffers.Add(new ALaCarteDailyOffer { Date = EarlyInMonth, ALaCarteItemId = item.Id, Capacity = 0, OrderedCount = 0 });
        await db.SaveChangesAsync();

        var result = await sut.Handle(new GetALaCarteMonthlySummaryQuery(2026, 9), CancellationToken.None);

        Assert.Empty(result.OfferedItems);
    }

    [Fact]
    public async Task An_item_never_offered_this_month_is_not_in_offered_items()
    {
        await using var db = dbFactory.CreateDbContext();
        var item = new ALaCarteItem { Name = "Múlt havi köret", Category = ALaCarteCategory.Koret, PriceHuf = 500 };
        db.ALaCarteItems.Add(item);
        await db.SaveChangesAsync();
        // Csak a hónapon kívül (augusztus 31.) volt kínálva.
        db.ALaCarteDailyOffers.Add(new ALaCarteDailyOffer { Date = PrevMonth, ALaCarteItemId = item.Id, Capacity = 10, OrderedCount = 0 });
        await db.SaveChangesAsync();

        var result = await sut.Handle(new GetALaCarteMonthlySummaryQuery(2026, 9), CancellationToken.None);

        Assert.Empty(result.OfferedItems);
    }
}
