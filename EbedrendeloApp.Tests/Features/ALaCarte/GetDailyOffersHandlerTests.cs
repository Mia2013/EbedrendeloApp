using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.ALaCarte.GetDailyOffers;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Tests.Features.ALaCarte;

public class GetDailyOffersHandlerTests : IDisposable
{
    private readonly SqliteDbContextFactory dbFactory = new();
    private readonly GetDailyOffersHandler sut;
    private static readonly DateOnly Today = new(2026, 9, 1);
    private static readonly DateOnly Tomorrow = new(2026, 9, 2);

    public GetDailyOffersHandlerTests() => sut = new GetDailyOffersHandler(dbFactory);

    public void Dispose() => dbFactory.Dispose();

    [Fact]
    public async Task Returns_offers_for_the_requested_date_including_soup()
    {
        await using (var db = dbFactory.CreateDbContext())
        {
            var soup = new ALaCarteItem { Name = "Csontleves", Category = ALaCarteCategory.Leves, PriceHuf = 400 };
            var main = new ALaCarteItem { Name = "Rántott szelet", Category = ALaCarteCategory.Foetel, PriceHuf = 1500 };
            var otherDayItem = new ALaCarteItem { Name = "Palacsinta", Category = ALaCarteCategory.Desszert, PriceHuf = 600 };
            db.ALaCarteItems.AddRange(soup, main, otherDayItem);
            await db.SaveChangesAsync();

            db.ALaCarteDailyOffers.AddRange(
                new ALaCarteDailyOffer { Date = Today, ALaCarteItemId = soup.Id, Capacity = int.MaxValue, OrderedCount = 0 },
                new ALaCarteDailyOffer { Date = Today, ALaCarteItemId = main.Id, Capacity = 10, OrderedCount = 4 },
                new ALaCarteDailyOffer { Date = Tomorrow, ALaCarteItemId = otherDayItem.Id, Capacity = 5, OrderedCount = 0 });
            await db.SaveChangesAsync();
        }

        var result = await sut.Handle(new GetDailyOffersQuery(Today), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, o => o.ItemName == "Csontleves");
        var mainOffer = Assert.Single(result, o => o.ItemName == "Rántott szelet");
        Assert.Equal(6, mainOffer.FreeCount);
    }

    [Fact]
    public async Task Returns_empty_when_there_are_no_offers_for_the_date()
    {
        var result = await sut.Handle(new GetDailyOffersQuery(Today), CancellationToken.None);

        Assert.Empty(result);
    }
}
