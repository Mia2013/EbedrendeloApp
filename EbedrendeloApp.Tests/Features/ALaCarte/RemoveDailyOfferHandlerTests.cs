using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.ALaCarte.RemoveDailyOffer;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Tests.Features.ALaCarte;

public class RemoveDailyOfferHandlerTests : IDisposable
{
    private readonly SqliteDbContextFactory dbFactory = new();
    private readonly RemoveDailyOfferHandler sut;
    private static readonly DateOnly Today = new(2026, 9, 1);

    public RemoveDailyOfferHandlerTests() => sut = new RemoveDailyOfferHandler(dbFactory);

    public void Dispose() => dbFactory.Dispose();

    [Fact]
    public async Task Removes_an_untouched_offer()
    {
        int offerId;
        await using (var db = dbFactory.CreateDbContext())
        {
            var item = new ALaCarteItem { Name = "Rántott szelet", Category = ALaCarteCategory.Foetel, PriceHuf = 500 };
            db.ALaCarteItems.Add(item);
            await db.SaveChangesAsync();
            var offer = new ALaCarteDailyOffer { Date = Today, ALaCarteItemId = item.Id, Capacity = 10, OrderedCount = 0 };
            db.ALaCarteDailyOffers.Add(offer);
            await db.SaveChangesAsync();
            offerId = offer.Id;
        }

        var result = await sut.Handle(new RemoveDailyOfferCommand(offerId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        await using var db2 = dbFactory.CreateDbContext();
        Assert.Empty(db2.ALaCarteDailyOffers);
    }

    [Fact]
    public async Task Rejects_removal_when_the_offer_already_has_orders()
    {
        int offerId;
        await using (var db = dbFactory.CreateDbContext())
        {
            var item = new ALaCarteItem { Name = "Rántott szelet", Category = ALaCarteCategory.Foetel, PriceHuf = 500 };
            db.ALaCarteItems.Add(item);
            await db.SaveChangesAsync();
            var offer = new ALaCarteDailyOffer { Date = Today, ALaCarteItemId = item.Id, Capacity = 10, OrderedCount = 3 };
            db.ALaCarteDailyOffers.Add(offer);
            await db.SaveChangesAsync();
            offerId = offer.Id;
        }

        var result = await sut.Handle(new RemoveDailyOfferCommand(offerId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.HasOrders, result.ErrorCode);
    }

    [Fact]
    public async Task Rejects_an_unknown_offer_id()
    {
        var result = await sut.Handle(new RemoveDailyOfferCommand(999), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NotFound, result.ErrorCode);
    }
}
