using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.ALaCarte.SetDailyOffer;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Tests.Features.ALaCarte;

public class SetDailyOfferHandlerTests : IDisposable
{
    private readonly SqliteDbContextFactory dbFactory = new();
    private readonly SetDailyOfferHandler sut;
    private static readonly DateOnly Today = new(2026, 9, 1);

    public SetDailyOfferHandlerTests() => sut = new SetDailyOfferHandler(dbFactory);

    public void Dispose() => dbFactory.Dispose();

    private async Task<int> SeedItemAsync(string name, ALaCarteCategory category, bool isActive = true)
    {
        await using var db = dbFactory.CreateDbContext();
        var item = new ALaCarteItem { Name = name, Category = category, PriceHuf = 500, IsActive = isActive };
        db.ALaCarteItems.Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    }

    [Fact]
    public async Task Creates_a_new_offer_for_a_non_soup_item()
    {
        var itemId = await SeedItemAsync("Rántott szelet", ALaCarteCategory.Foetel);

        var result = await sut.Handle(new SetDailyOfferCommand(Today, itemId, 10), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(10, result.Value!.Capacity);
        Assert.Equal(10, result.Value.FreeCount);
    }

    [Fact]
    public async Task Updates_the_capacity_of_an_existing_offer()
    {
        var itemId = await SeedItemAsync("Rántott szelet", ALaCarteCategory.Foetel);
        await sut.Handle(new SetDailyOfferCommand(Today, itemId, 10), CancellationToken.None);

        var result = await sut.Handle(new SetDailyOfferCommand(Today, itemId, 20), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(20, result.Value!.Capacity);

        await using var db = dbFactory.CreateDbContext();
        Assert.Single(db.ALaCarteDailyOffers);
    }

    [Fact]
    public async Task Rejects_an_unknown_or_inactive_item()
    {
        var inactiveId = await SeedItemAsync("Kifutott tétel", ALaCarteCategory.Koret, isActive: false);

        var result = await sut.Handle(new SetDailyOfferCommand(Today, inactiveId, 5), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Forces_unlimited_capacity_for_a_soup_item()
    {
        var soupId = await SeedItemAsync("Csontleves", ALaCarteCategory.Leves);

        var result = await sut.Handle(new SetDailyOfferCommand(Today, soupId, 5), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(int.MaxValue, result.Value!.Capacity);
    }

    [Fact]
    public async Task Rejects_a_second_soup_offer_on_the_same_day()
    {
        var soup1 = await SeedItemAsync("Csontleves", ALaCarteCategory.Leves);
        var soup2 = await SeedItemAsync("Gulyásleves", ALaCarteCategory.Leves);
        await sut.Handle(new SetDailyOfferCommand(Today, soup1, 0), CancellationToken.None);

        var result = await sut.Handle(new SetDailyOfferCommand(Today, soup2, 0), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.SoupAlreadyOffered, result.ErrorCode);
    }

    [Fact]
    public async Task Rejects_lowering_capacity_below_already_reserved_count()
    {
        var itemId = await SeedItemAsync("Rántott szelet", ALaCarteCategory.Foetel);
        await using (var db = dbFactory.CreateDbContext())
        {
            db.ALaCarteDailyOffers.Add(new ALaCarteDailyOffer { Date = Today, ALaCarteItemId = itemId, Capacity = 10, OrderedCount = 7 });
            await db.SaveChangesAsync();
        }

        var result = await sut.Handle(new SetDailyOfferCommand(Today, itemId, 5), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.CapacityBelowReserved, result.ErrorCode);
    }
}
