using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Features.Billing.GetInvoices;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Tests.Features.Billing;

public class GetInvoicesHandlerTests : IDisposable
{
    private readonly SqliteDbContextFactory dbFactory = new();
    private readonly GetInvoicesHandler sut;

    public GetInvoicesHandlerTests() => sut = new GetInvoicesHandler(dbFactory);

    public void Dispose() => dbFactory.Dispose();

    private async Task<(int period1Id, int period2Id, int user1Id, int user2Id)> SeedAsync()
    {
        await using var db = dbFactory.CreateDbContext();

        var period1 = new OrderingPeriod { Name = "Augusztus", StartDate = new DateOnly(2026, 8, 1), EndDate = new DateOnly(2026, 8, 31), OrderDeadline = new DateTime(2026, 7, 15, 10, 0, 0) };
        var period2 = new OrderingPeriod { Name = "Szeptember", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 9, 30), OrderDeadline = new DateTime(2026, 8, 15, 10, 0, 0) };
        db.OrderingPeriods.AddRange(period1, period2);

        var role = new Role { Name = "User" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var user1 = new User { UserId = 1, UserName = "u1", VezetekNev = "Kis", KeresztNev = "Anna", RoleId = role.Id };
        var user2 = new User { UserId = 2, UserName = "u2", VezetekNev = "Nagy", KeresztNev = "Béla", RoleId = role.Id };
        db.Users.AddRange(user1, user2);
        await db.SaveChangesAsync();

        db.PeriodInvoices.AddRange(
            new PeriodInvoice
            {
                UserId = user1.Id, OrderingPeriodId = period1.Id, MenuGrossHuf = 1400, ALaCarteGrossHuf = 0, GrossHuf = 1400,
                CreditAppliedHuf = 0, MenuPayableHuf = 1400, ALaCartePayableHuf = 0, PayableHuf = 1400, IsPaid = true,
                GeneratedAtUtc = new DateTime(2026, 7, 20, 9, 0, 0),
            },
            new PeriodInvoice
            {
                UserId = user2.Id, OrderingPeriodId = period2.Id, MenuGrossHuf = 2800, ALaCarteGrossHuf = 500, GrossHuf = 3300,
                CreditAppliedHuf = 300, MenuPayableHuf = 2500, ALaCartePayableHuf = 500, PayableHuf = 3000, IsPaid = false,
                GeneratedAtUtc = new DateTime(2026, 8, 20, 9, 0, 0),
            });
        await db.SaveChangesAsync();

        return (period1.Id, period2.Id, user1.Id, user2.Id);
    }

    [Fact]
    public async Task Filters_by_ordering_period_and_paid_status()
    {
        var (_, period2Id, user1Id, user2Id) = await SeedAsync();

        var all = await sut.Handle(new GetInvoicesQuery(null, null), CancellationToken.None);
        Assert.Equal(2, all.Value!.Count);

        var period2Only = await sut.Handle(new GetInvoicesQuery(period2Id, null), CancellationToken.None);
        var period2Invoice = Assert.Single(period2Only.Value!);
        Assert.Equal(user2Id, period2Invoice.UserId);
        Assert.Equal("Nagy Béla", period2Invoice.UserDisplayName);
        Assert.Equal("Szeptember", period2Invoice.PeriodName);
        Assert.Equal(2800, period2Invoice.MenuGrossHuf);
        Assert.Equal(500, period2Invoice.ALaCarteGrossHuf);
        Assert.Equal(300, period2Invoice.CreditAppliedHuf);
        Assert.Equal(2500, period2Invoice.MenuPayableHuf);
        Assert.Equal(500, period2Invoice.ALaCartePayableHuf);
        Assert.Equal(3000, period2Invoice.PayableHuf);

        var unpaidOnly = await sut.Handle(new GetInvoicesQuery(null, false), CancellationToken.None);
        Assert.Equal(user2Id, Assert.Single(unpaidOnly.Value!).UserId);

        var paidOnly = await sut.Handle(new GetInvoicesQuery(null, true), CancellationToken.None);
        Assert.Equal(user1Id, Assert.Single(paidOnly.Value!).UserId);
    }
}
