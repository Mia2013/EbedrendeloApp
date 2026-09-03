using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.ALaCarte.SetALaCarteItemActive;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Tests.Features.ALaCarte;

public class SetALaCarteItemActiveHandlerTests : IDisposable
{
    private readonly SqliteDbContextFactory dbFactory = new();
    private readonly SetALaCarteItemActiveHandler sut;

    public SetALaCarteItemActiveHandlerTests() => sut = new SetALaCarteItemActiveHandler(dbFactory);

    public void Dispose() => dbFactory.Dispose();

    [Fact]
    public async Task Sets_IsActive_to_false()
    {
        int itemId;
        await using (var db = dbFactory.CreateDbContext())
        {
            var item = new ALaCarteItem { Name = "Tartár mártás", Category = ALaCarteCategory.Ontet, PriceHuf = 350, IsActive = true };
            db.ALaCarteItems.Add(item);
            await db.SaveChangesAsync();
            itemId = item.Id;
        }

        var result = await sut.Handle(new SetALaCarteItemActiveCommand(itemId, false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        await using var verifyDb = dbFactory.CreateDbContext();
        Assert.False((await verifyDb.ALaCarteItems.SingleAsync(i => i.Id == itemId)).IsActive);
    }

    [Fact]
    public async Task Sets_IsActive_back_to_true()
    {
        int itemId;
        await using (var db = dbFactory.CreateDbContext())
        {
            var item = new ALaCarteItem { Name = "Tartár mártás", Category = ALaCarteCategory.Ontet, PriceHuf = 350, IsActive = false };
            db.ALaCarteItems.Add(item);
            await db.SaveChangesAsync();
            itemId = item.Id;
        }

        var result = await sut.Handle(new SetALaCarteItemActiveCommand(itemId, true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        await using var verifyDb = dbFactory.CreateDbContext();
        Assert.True((await verifyDb.ALaCarteItems.SingleAsync(i => i.Id == itemId)).IsActive);
    }

    [Fact]
    public async Task Rejects_an_unknown_id()
    {
        var result = await sut.Handle(new SetALaCarteItemActiveCommand(999, false), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NotFound, result.ErrorCode);
    }
}
