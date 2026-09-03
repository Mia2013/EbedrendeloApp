using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.ALaCarte.GetALaCarteItems;
using EbedrendeloApp.Tests.TestSupport;

namespace EbedrendeloApp.Tests.Features.ALaCarte;

public class GetALaCarteItemsHandlerTests : IDisposable
{
    private readonly SqliteDbContextFactory dbFactory = new();
    private readonly GetALaCarteItemsHandler sut;

    public GetALaCarteItemsHandlerTests() => sut = new GetALaCarteItemsHandler(dbFactory);

    public void Dispose() => dbFactory.Dispose();

    [Fact]
    public async Task Returns_active_and_inactive_items_ordered_by_category_then_name()
    {
        await using (var db = dbFactory.CreateDbContext())
        {
            db.ALaCarteItems.AddRange(
                new ALaCarteItem { Name = "Túrós derelye", Category = ALaCarteCategory.Desszert, PriceHuf = 750, IsActive = true },
                new ALaCarteItem { Name = "Csontleves", Category = ALaCarteCategory.Leves, PriceHuf = 650, IsActive = true },
                new ALaCarteItem { Name = "Régi köret", Category = ALaCarteCategory.Koret, PriceHuf = 400, IsActive = false });
            await db.SaveChangesAsync();
        }

        var result = await sut.Handle(new GetALaCarteItemsQuery(), CancellationToken.None);

        Assert.Equal(3, result.Count);
        Assert.Equal(["Csontleves", "Régi köret", "Túrós derelye"], result.Select(i => i.Name)); // Leves(0) < Koret(2) < Desszert(3)
        Assert.False(result.Single(i => i.Name == "Régi köret").IsActive);
    }
}
