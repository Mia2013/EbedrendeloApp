using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Features.Kitchen.ReopenDay;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Tests.Features.Kitchen;

public class ReopenDayHandlerTests : IDisposable
{
    private static readonly DateOnly Thu = new(2026, 8, 20);

    private readonly SqliteDbContextFactory dbFactory = new();
    private readonly FixedAppClock clock = new(new DateTime(2026, 8, 20, 15, 0, 0));
    private readonly ReopenDayHandler sut;

    private int adminId;

    public ReopenDayHandlerTests() => sut = new ReopenDayHandler(dbFactory, clock);

    public void Dispose() => dbFactory.Dispose();

    [Fact]
    public async Task Reopens_a_closed_day_without_touching_the_original_snapshot()
    {
        var closureId = await SeedClosedDayAsync();

        var result = await sut.Handle(new ReopenDayCommand(Thu, adminId), CancellationToken.None);

        Assert.True(result.IsSuccess);

        await using var db = dbFactory.CreateDbContext();
        var reopening = await db.KitchenClosureReopenings.SingleAsync(r => r.KitchenClosureId == closureId);
        Assert.Equal(adminId, reopening.ReopenedByUserId);
        Assert.Equal(clock.UtcNow.UtcDateTime, reopening.ReopenedAtUtc);

        // The original closure and its lines must be untouched — the reopen only ever adds a row.
        var closure = await db.KitchenClosures.Include(k => k.Lines).SingleAsync(k => k.Id == closureId);
        Assert.Equal(2, closure.TotalPortions);
        Assert.Single(closure.Lines);
        Assert.Equal("A", closure.Lines[0].VariantCode);
        Assert.Equal(2, closure.Lines[0].Quantity);
    }

    [Fact]
    public async Task Rejects_a_day_that_was_never_closed()
    {
        await SeedUsersAsync();

        var result = await sut.Handle(new ReopenDayCommand(Thu, adminId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Rejects_a_day_whose_only_closure_is_already_reopened()
    {
        await SeedClosedDayAsync();
        var first = await sut.Handle(new ReopenDayCommand(Thu, adminId), CancellationToken.None);
        Assert.True(first.IsSuccess);

        var second = await sut.Handle(new ReopenDayCommand(Thu, adminId), CancellationToken.None);

        Assert.False(second.IsSuccess);
        Assert.Equal(ErrorCodes.NotFound, second.ErrorCode);
    }

    private async Task SeedUsersAsync()
    {
        await using var db = dbFactory.CreateDbContext();
        var role = new Role { Name = "Admin" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var admin = new User { UserId = 1, UserName = "admin", RoleId = role.Id };
        db.Users.Add(admin);
        await db.SaveChangesAsync();
        adminId = admin.Id;
    }

    private async Task<int> SeedClosedDayAsync()
    {
        await SeedUsersAsync();

        await using var db = dbFactory.CreateDbContext();
        var closure = new KitchenClosure
        {
            Date = Thu,
            ClosedAtUtc = clock.UtcNow.UtcDateTime,
            ClosedByUserId = adminId,
            TotalPortions = 2,
        };
        closure.Lines.Add(new KitchenClosureLine { KitchenClosureId = 0, VariantCode = "A", VariantNameSnapshot = "Gulyásleves + Rántott hús", Quantity = 2 });
        db.KitchenClosures.Add(closure);
        await db.SaveChangesAsync();
        return closure.Id;
    }
}
