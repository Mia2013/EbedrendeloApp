using EbedrendeloApp.Common.Calendar;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Calendar.UpsertOrderingPeriod;
using EbedrendeloApp.Tests.TestSupport;

namespace EbedrendeloApp.Tests.Features.Calendar;

public class UpsertOrderingPeriodHandlerTests : IDisposable
{
    private readonly SqliteDbContextFactory dbFactory = new();
    private readonly UpsertOrderingPeriodHandler sut;

    public UpsertOrderingPeriodHandlerTests()
    {
        sut = new UpsertOrderingPeriodHandler(dbFactory, new WorkingDayCalculator());

        using var db = dbFactory.CreateDbContext();
        db.AppSettings.Add(new AppSetting
        {
            MenuPortionHuf = 1400,
            ChangeDeadlineWorkingDays = 3,
            ChangeDeadlineLocalTime = new TimeOnly(11, 0),
            ALaCarteOrderDeadlineLocalTime = new TimeOnly(10, 30),
        });
        db.SaveChanges();
    }

    public void Dispose() => dbFactory.Dispose();

    [Fact]
    public async Task Rejects_fully_overlapping_period()
    {
        await SeedPeriodAsync(new DateOnly(2026, 8, 5), new DateOnly(2026, 9, 5));

        var result = await sut.Handle(
            new UpsertOrderingPeriodCommand(null, "2026. augusztus vol2", new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 20),
                new DateTime(2026, 7, 20, 10, 0, 0), true),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.Overlaps, result.ErrorCode);
    }

    [Fact]
    public async Task Rejects_partially_overlapping_period()
    {
        await SeedPeriodAsync(new DateOnly(2026, 8, 5), new DateOnly(2026, 9, 5));

        var result = await sut.Handle(
            new UpsertOrderingPeriodCommand(null, "Átfedő", new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 1),
                new DateTime(2026, 8, 20, 10, 0, 0), true),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.Overlaps, result.ErrorCode);
    }

    [Fact]
    public async Task Accepts_a_period_starting_the_day_after_the_previous_one_ends()
    {
        await SeedPeriodAsync(new DateOnly(2026, 8, 5), new DateOnly(2026, 9, 5));

        var result = await sut.Handle(
            new UpsertOrderingPeriodCommand(null, "Szeptember", new DateOnly(2026, 9, 6), new DateOnly(2026, 10, 6),
                new DateTime(2026, 8, 27, 10, 0, 0), true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Rejects_a_period_sharing_a_boundary_date_with_an_existing_one()
    {
        // The overlap formula (StartDate <= p.EndDate && EndDate >= p.StartDate) treats a shared
        // boundary date as an overlap — the next period must start the day *after* EndDate, not on it.
        await SeedPeriodAsync(new DateOnly(2026, 8, 5), new DateOnly(2026, 9, 5));

        var result = await sut.Handle(
            new UpsertOrderingPeriodCommand(null, "Szeptember", new DateOnly(2026, 9, 5), new DateOnly(2026, 10, 5),
                new DateTime(2026, 8, 26, 10, 0, 0), true),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.Overlaps, result.ErrorCode);
    }

    [Fact]
    public async Task Rejects_order_deadline_after_change_deadline_of_start_date()
    {
        // StartDate Monday 2026-09-07 -> ChangeDeadline = Wednesday 2026-09-02 11:00.
        var result = await sut.Handle(
            new UpsertOrderingPeriodCommand(null, "Szeptember", new DateOnly(2026, 9, 7), new DateOnly(2026, 10, 7),
                new DateTime(2026, 9, 2, 11, 1, 0), true),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.DeadlinePassed, result.ErrorCode);
    }

    [Fact]
    public async Task Accepts_order_deadline_exactly_at_change_deadline_of_start_date()
    {
        var result = await sut.Handle(
            new UpsertOrderingPeriodCommand(null, "Szeptember", new DateOnly(2026, 9, 7), new DateOnly(2026, 10, 7),
                new DateTime(2026, 9, 2, 11, 0, 0), true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Locks_date_fields_once_the_period_has_orders()
    {
        var periodId = await SeedPeriodAsync(new DateOnly(2026, 8, 5), new DateOnly(2026, 9, 5));

        await SeedOrderAsync(periodId, new DateOnly(2026, 8, 10));

        var result = await sut.Handle(
            new UpsertOrderingPeriodCommand(periodId, "Átnevezve", new DateOnly(2026, 8, 6), new DateOnly(2026, 9, 5),
                new DateTime(2026, 7, 26, 10, 0, 0), true),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.HasOrders, result.ErrorCode);
    }

    [Fact]
    public async Task Allows_renaming_and_toggling_open_state_once_the_period_has_orders()
    {
        var periodId = await SeedPeriodAsync(new DateOnly(2026, 8, 5), new DateOnly(2026, 9, 5));

        await SeedOrderAsync(periodId, new DateOnly(2026, 8, 10));

        var result = await sut.Handle(
            new UpsertOrderingPeriodCommand(periodId, "Átnevezve", new DateOnly(2026, 8, 5), new DateOnly(2026, 9, 5),
                new DateTime(2026, 7, 26, 10, 0, 0), false),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Átnevezve", result.Value!.Name);
        Assert.False(result.Value.IsOpen);
    }

    private async Task<int> SeedPeriodAsync(DateOnly start, DateOnly end)
    {
        using var db = dbFactory.CreateDbContext();
        var period = new OrderingPeriod
        {
            Name = "Meglévő",
            StartDate = start,
            EndDate = end,
            OrderDeadline = start.AddDays(-10).ToDateTime(new TimeOnly(10, 0)),
            IsOpen = true,
        };
        db.OrderingPeriods.Add(period);
        await db.SaveChangesAsync();
        return period.Id;
    }

    private async Task SeedOrderAsync(int periodId, DateOnly date)
    {
        using var db = dbFactory.CreateDbContext();

        var dish = new MenuDish { Kind = MenuDishKind.Leves, Name = "Teszt menü" };
        db.MenuDishes.Add(dish);
        await db.SaveChangesAsync();

        var dailyMenu = new DailyMenu { Date = date, IsPublished = true };
        dailyMenu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "A", SoupName = "Teszt menü", SoupDishId = dish.Id, SortOrder = 0 });
        db.DailyMenus.Add(dailyMenu);

        var role = new Role { Name = "User" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var user = new User { UserId = 1, UserName = "u1", RoleId = role.Id };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.MenuOrders.Add(new MenuOrder
        {
            UserId = user.Id,
            Date = date,
            OrderingPeriodId = periodId,
            MenuVariantId = dailyMenu.Variants[0].Id,
            PriceHuf = 1400,
            Status = OrderStatus.Active,
            PlacedByUserId = user.Id,
        });
        await db.SaveChangesAsync();
    }
}
