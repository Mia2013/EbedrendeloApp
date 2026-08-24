using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Features.Menus.GetDailyMenu;
using EbedrendeloApp.Tests.TestSupport;

namespace EbedrendeloApp.Tests.Features.Menus;

public class GetDailyMenuHandlerTests : IDisposable
{
    private readonly SqliteDbContextFactory dbFactory = new();
    private readonly GetDailyMenuHandler sut;

    public GetDailyMenuHandlerTests()
    {
        sut = new GetDailyMenuHandler(dbFactory);
    }

    public void Dispose() => dbFactory.Dispose();

    [Fact]
    public async Task Returns_null_when_there_is_no_menu()
    {
        var result = await sut.Handle(new GetDailyMenuQuery(new DateOnly(2026, 8, 20), IncludeUnpublished: true), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Hides_unpublished_menu_from_non_admin_caller()
    {
        var date = new DateOnly(2026, 8, 20);
        await SeedMenuAsync(date, isPublished: false);

        var result = await sut.Handle(new GetDailyMenuQuery(date, IncludeUnpublished: false), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Shows_unpublished_menu_to_admin_caller()
    {
        var date = new DateOnly(2026, 8, 20);
        await SeedMenuAsync(date, isPublished: false);

        var result = await sut.Handle(new GetDailyMenuQuery(date, IncludeUnpublished: true), CancellationToken.None);

        Assert.NotNull(result);
        Assert.False(result!.IsPublished);
    }

    [Fact]
    public async Task Excludes_soft_deleted_variants_and_orders_by_sort_order_then_code()
    {
        var date = new DateOnly(2026, 8, 20);
        await using (var db = dbFactory.CreateDbContext())
        {
            var menu = new DailyMenu { Date = date, IsPublished = true };
            menu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "B", Name = "B menü", SortOrder = 1 });
            menu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "A", Name = "A menü", SortOrder = 0 });
            menu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "C", Name = "Törölt", SortOrder = 2, RemovedAtUtc = DateTime.UtcNow });
            db.DailyMenus.Add(menu);
            await db.SaveChangesAsync();
        }

        var result = await sut.Handle(new GetDailyMenuQuery(date, IncludeUnpublished: true), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(["A", "B"], result!.Variants.Select(v => v.Code));
    }

    private async Task SeedMenuAsync(DateOnly date, bool isPublished)
    {
        await using var db = dbFactory.CreateDbContext();
        var menu = new DailyMenu { Date = date, IsPublished = isPublished };
        menu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "A", Name = "Menü", SortOrder = 0 });
        db.DailyMenus.Add(menu);
        await db.SaveChangesAsync();
    }
}
