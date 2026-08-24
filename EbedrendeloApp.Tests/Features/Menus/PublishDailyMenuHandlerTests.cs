using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Features.Menus.PublishDailyMenu;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Tests.Features.Menus;

public class PublishDailyMenuHandlerTests : IDisposable
{
    private readonly SqliteDbContextFactory dbFactory = new();
    private readonly PublishDailyMenuHandler sut;

    public PublishDailyMenuHandlerTests()
    {
        sut = new PublishDailyMenuHandler(dbFactory);
    }

    public void Dispose() => dbFactory.Dispose();

    [Fact]
    public async Task Rejects_when_there_is_no_menu_for_the_day()
    {
        var result = await sut.Handle(new PublishDailyMenuCommand(new DateOnly(2026, 8, 20)), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Rejects_when_the_menu_has_no_variants()
    {
        var date = new DateOnly(2026, 8, 20);
        await using (var db = dbFactory.CreateDbContext())
        {
            db.DailyMenus.Add(new DailyMenu { Date = date, IsPublished = false });
            await db.SaveChangesAsync();
        }

        var result = await sut.Handle(new PublishDailyMenuCommand(date), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NoVariants, result.ErrorCode);
    }

    [Fact]
    public async Task Publishes_a_menu_that_has_variants()
    {
        var date = new DateOnly(2026, 8, 20);
        int menuId;
        await using (var db = dbFactory.CreateDbContext())
        {
            var menu = new DailyMenu { Date = date, IsPublished = false };
            menu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "A", Name = "Menü", SortOrder = 0 });
            db.DailyMenus.Add(menu);
            await db.SaveChangesAsync();
            menuId = menu.Id;
        }

        var result = await sut.Handle(new PublishDailyMenuCommand(date), CancellationToken.None);

        Assert.True(result.IsSuccess);

        await using var db2 = dbFactory.CreateDbContext();
        var menu2 = await db2.DailyMenus.SingleAsync(m => m.Id == menuId);
        Assert.True(menu2.IsPublished);
    }
}
