using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Features.Kitchen.GetKitchenClosure;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Tests.Features.Kitchen;

public class GetKitchenClosureHandlerTests : IDisposable
{
    private static readonly DateOnly Thu = new(2026, 8, 20);

    private readonly SqliteDbContextFactory dbFactory = new();
    private readonly GetKitchenClosureHandler sut;

    private int closerId;
    private int reopenerId;

    public GetKitchenClosureHandlerTests() => sut = new GetKitchenClosureHandler(dbFactory);

    public void Dispose() => dbFactory.Dispose();

    [Fact]
    public async Task Returns_null_when_the_day_was_never_closed()
    {
        await SeedUsersAsync();

        var result = await sut.Handle(new GetKitchenClosureQuery(Thu), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Returns_the_snapshot_with_lines_and_the_closing_user_name()
    {
        await SeedUsersAsync();
        await using (var db = dbFactory.CreateDbContext())
        {
            var closure = new KitchenClosure { Date = Thu, ClosedByUserId = closerId, TotalPortions = 2 };
            closure.Lines.Add(new KitchenClosureLine { KitchenClosureId = 0, VariantCode = "A", VariantNameSnapshot = "Gulyásleves + Rántott hús", Quantity = 2 });
            db.KitchenClosures.Add(closure);
            await db.SaveChangesAsync();
        }

        var result = await sut.Handle(new GetKitchenClosureQuery(Thu), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, result.TotalPortions);
        Assert.Single(result.Lines);
        Assert.Equal("Gulyásleves + Rántott hús", result.Lines[0].VariantNameSnapshot);
        Assert.Equal("Zárás Admin", result.ClosedByUserName);
        Assert.Null(result.ReopenedAtUtc);
        Assert.Null(result.ReopenedByUserName);
    }

    [Fact]
    public async Task Reflects_the_reopening_while_still_showing_the_original_snapshot()
    {
        await SeedUsersAsync();
        int closureId;
        await using (var db = dbFactory.CreateDbContext())
        {
            var closure = new KitchenClosure { Date = Thu, ClosedByUserId = closerId, TotalPortions = 1 };
            closure.Lines.Add(new KitchenClosureLine { KitchenClosureId = 0, VariantCode = "A", VariantNameSnapshot = "Gulyásleves", Quantity = 1 });
            db.KitchenClosures.Add(closure);
            await db.SaveChangesAsync();
            closureId = closure.Id;
        }

        await using (var db = dbFactory.CreateDbContext())
        {
            db.KitchenClosureReopenings.Add(new KitchenClosureReopening { KitchenClosureId = closureId, ReopenedByUserId = reopenerId });
            await db.SaveChangesAsync();
        }

        var result = await sut.Handle(new GetKitchenClosureQuery(Thu), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Single(result.Lines); // untouched snapshot
        Assert.NotNull(result.ReopenedAtUtc);
        Assert.Equal(reopenerId, result.ReopenedByUserId);
    }

    [Fact]
    public async Task Returns_the_most_recent_closure_after_multiple_close_reopen_cycles()
    {
        await SeedUsersAsync();

        await using (var db = dbFactory.CreateDbContext())
        {
            var firstClosure = new KitchenClosure { Date = Thu, ClosedByUserId = closerId, TotalPortions = 1, ClosedAtUtc = new DateTime(2026, 8, 20, 10, 0, 0) };
            firstClosure.Lines.Add(new KitchenClosureLine { KitchenClosureId = 0, VariantCode = "A", VariantNameSnapshot = "Első zárás", Quantity = 1 });
            db.KitchenClosures.Add(firstClosure);
            await db.SaveChangesAsync();

            db.KitchenClosureReopenings.Add(new KitchenClosureReopening { KitchenClosureId = firstClosure.Id, ReopenedByUserId = reopenerId });

            var secondClosure = new KitchenClosure { Date = Thu, ClosedByUserId = closerId, TotalPortions = 3, ClosedAtUtc = new DateTime(2026, 8, 20, 14, 0, 0) };
            secondClosure.Lines.Add(new KitchenClosureLine { KitchenClosureId = 0, VariantCode = "A", VariantNameSnapshot = "Második zárás", Quantity = 3 });
            db.KitchenClosures.Add(secondClosure);

            await db.SaveChangesAsync();
        }

        var result = await sut.Handle(new GetKitchenClosureQuery(Thu), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(3, result.TotalPortions);
        Assert.Equal("Második zárás", result.Lines[0].VariantNameSnapshot);
        Assert.Null(result.ReopenedAtUtc); // the latest closure was never reopened
    }

    private async Task SeedUsersAsync()
    {
        await using var db = dbFactory.CreateDbContext();
        var role = new Role { Name = "Admin" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var closer = new User { UserId = 1, UserName = "closer", VezetekNev = "Zárás", KeresztNev = "Admin", RoleId = role.Id };
        var reopener = new User { UserId = 2, UserName = "reopener", VezetekNev = "Nyitás", KeresztNev = "Admin", RoleId = role.Id };
        db.Users.AddRange(closer, reopener);
        await db.SaveChangesAsync();
        closerId = closer.Id;
        reopenerId = reopener.Id;
    }
}
