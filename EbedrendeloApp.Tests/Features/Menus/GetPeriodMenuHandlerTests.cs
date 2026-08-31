using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Menus.GetPeriodMenu;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Tests.Features.Menus;

public class GetPeriodMenuHandlerTests : IDisposable
{
    private readonly SqliteDbContextFactory dbFactory = new();
    private readonly GetPeriodMenuHandler sut;

    public GetPeriodMenuHandlerTests()
    {
        sut = new GetPeriodMenuHandler(dbFactory);
    }

    public void Dispose() => dbFactory.Dispose();

    [Fact]
    public async Task Rejects_unknown_period()
    {
        var result = await sut.Handle(new GetPeriodMenuQuery(999, IncludeUnpublished: true), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Returns_only_days_within_the_period_range()
    {
        var periodId = await SeedPeriodAsync(new DateOnly(2026, 8, 5), new DateOnly(2026, 9, 5));
        await SeedMenuAsync(new DateOnly(2026, 8, 10), isPublished: true);
        await SeedMenuAsync(new DateOnly(2026, 9, 10), isPublished: true); // outside range

        var result = await sut.Handle(new GetPeriodMenuQuery(periodId, IncludeUnpublished: true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var day = Assert.Single(result.Value!);
        Assert.Equal(new DateOnly(2026, 8, 10), day.Date);
    }

    [Fact]
    public async Task Filters_out_unpublished_days_for_non_admin_caller()
    {
        var periodId = await SeedPeriodAsync(new DateOnly(2026, 8, 5), new DateOnly(2026, 9, 5));
        await SeedMenuAsync(new DateOnly(2026, 8, 10), isPublished: true);
        await SeedMenuAsync(new DateOnly(2026, 8, 11), isPublished: false);

        var result = await sut.Handle(new GetPeriodMenuQuery(periodId, IncludeUnpublished: false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var day = Assert.Single(result.Value!);
        Assert.Equal(new DateOnly(2026, 8, 10), day.Date);
    }

    private async Task<int> SeedPeriodAsync(DateOnly start, DateOnly end)
    {
        await using var db = dbFactory.CreateDbContext();
        var period = new OrderingPeriod
        {
            Name = "Teszt időszak",
            StartDate = start,
            EndDate = end,
            OrderDeadline = start.AddDays(-10).ToDateTime(new TimeOnly(10, 0)),
        };
        db.OrderingPeriods.Add(period);
        await db.SaveChangesAsync();
        return period.Id;
    }

    private async Task SeedMenuAsync(DateOnly date, bool isPublished)
    {
        await using var db = dbFactory.CreateDbContext();
        var dish = await db.MenuDishes.SingleOrDefaultAsync(d => d.Kind == MenuDishKind.Leves && d.Name == "Menü");
        if (dish is null)
        {
            dish = new MenuDish { Kind = MenuDishKind.Leves, Name = "Menü" };
            db.MenuDishes.Add(dish);
            await db.SaveChangesAsync();
        }

        var menu = new DailyMenu { Date = date, IsPublished = isPublished };
        menu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "A", SoupName = "Menü", SoupDishId = dish.Id, SortOrder = 0 });
        db.DailyMenus.Add(menu);
        await db.SaveChangesAsync();
    }
}
