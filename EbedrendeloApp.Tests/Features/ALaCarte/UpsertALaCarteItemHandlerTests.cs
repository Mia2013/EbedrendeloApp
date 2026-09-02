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

    [Fact]
    public async Task Rejects_creating_a_second_item_with_the_same_name_in_the_same_category()
    {
        // Guards the race where two admins both type a brand-new item name before either saves — the
        // UI's own client-side name match can't see the other admin's not-yet-saved insert, so this
        // server-side check is the only thing that actually prevents the duplicate row.
        await sut.Handle(
            new UpsertALaCarteItemCommand(null, "Rántott sertés szelet", ALaCarteCategory.Foetel, 1900, true, []),
            CancellationToken.None);

        var result = await sut.Handle(
            new UpsertALaCarteItemCommand(null, "Rántott sertés szelet", ALaCarteCategory.Foetel, 1900, true, []),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.DuplicateName, result.ErrorCode);

        await using var db = dbFactory.CreateDbContext();
        Assert.Single(db.ALaCarteItems);
    }

    [Fact]
    public async Task Allows_the_same_name_in_a_different_category()
    {
        await sut.Handle(
            new UpsertALaCarteItemCommand(null, "Rizi-bizi", ALaCarteCategory.Koret, 500, true, []),
            CancellationToken.None);

        var result = await sut.Handle(
            new UpsertALaCarteItemCommand(null, "Rizi-bizi", ALaCarteCategory.Foetel, 1500, true, []),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        await using var db = dbFactory.CreateDbContext();
        Assert.Equal(2, db.ALaCarteItems.Count());
    }

    [Fact]
    public async Task Allows_saving_an_item_without_changing_its_own_name()
    {
        var created = await sut.Handle(
            new UpsertALaCarteItemCommand(null, "Rántott sertés szelet", ALaCarteCategory.Foetel, 1900, true, []),
            CancellationToken.None);

        var result = await sut.Handle(
            new UpsertALaCarteItemCommand(created.Value!.Id, "Rántott sertés szelet", ALaCarteCategory.Foetel, 2000, true, []),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2000, result.Value!.PriceHuf);
    }

    [Fact]
    public async Task Rejects_renaming_an_item_to_collide_with_another_existing_item()
    {
        await sut.Handle(
            new UpsertALaCarteItemCommand(null, "Rántott sertés szelet", ALaCarteCategory.Foetel, 1900, true, []),
            CancellationToken.None);
        var other = await sut.Handle(
            new UpsertALaCarteItemCommand(null, "Csirkemell", ALaCarteCategory.Foetel, 1800, true, []),
            CancellationToken.None);

        var result = await sut.Handle(
            new UpsertALaCarteItemCommand(other.Value!.Id, "Rántott sertés szelet", ALaCarteCategory.Foetel, 1800, true, []),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.DuplicateName, result.ErrorCode);
    }
}
