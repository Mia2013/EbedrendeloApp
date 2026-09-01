using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.ALaCarte.UpsertALaCarteItem;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Tests.Features.ALaCarte;

public class UpsertALaCarteItemHandlerTests : IDisposable
{
    private readonly SqliteDbContextFactory dbFactory = new();
    private readonly UpsertALaCarteItemHandler sut;

    public UpsertALaCarteItemHandlerTests() => sut = new UpsertALaCarteItemHandler(dbFactory);

    public void Dispose() => dbFactory.Dispose();

    [Fact]
    public async Task Creates_a_new_item_with_nutrition_and_allergens()
    {
        var result = await sut.Handle(
            new UpsertALaCarteItemCommand(null, "Rántott sertés szelet", ALaCarteCategory.Foetel, 1900, true, [1, 3], EnergyKcal: 780, ProteinGrams: 38),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Rántott sertés szelet", result.Value!.Name);
        Assert.Equal("1,3", result.Value.Allergens);
        Assert.Equal(780, result.Value.EnergyKcal);
        Assert.True(result.Value.IsActive);

        await using var db = dbFactory.CreateDbContext();
        Assert.Single(db.ALaCarteItems);
    }

    [Fact]
    public async Task Updates_an_existing_item_with_a_full_overwrite()
    {
        int itemId;
        await using (var db = dbFactory.CreateDbContext())
        {
            var item = new ALaCarteItem { Name = "Régi név", Category = ALaCarteCategory.Koret, PriceHuf = 500, Allergens = "1,9" };
            db.ALaCarteItems.Add(item);
            await db.SaveChangesAsync();
            itemId = item.Id;
        }

        var result = await sut.Handle(
            new UpsertALaCarteItemCommand(itemId, "Új név", ALaCarteCategory.Koret, 600, false, []),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Új név", result.Value!.Name);
        Assert.Equal(600, result.Value.PriceHuf);
        Assert.False(result.Value.IsActive);
        Assert.Null(result.Value.Allergens); // full overwrite clears the previously set allergens
    }

    [Fact]
    public async Task Rejects_an_update_for_an_unknown_id()
    {
        var result = await sut.Handle(
            new UpsertALaCarteItemCommand(999, "X", ALaCarteCategory.Koret, 500, true, []),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NotFound, result.ErrorCode);
    }
}
