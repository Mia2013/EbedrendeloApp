using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Features.Billing.GetMyInvoices;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Tests.Features.Billing;

public class GetMyInvoicesHandlerTests : IDisposable
{
    private readonly SqliteDbContextFactory dbFactory = new();
    private readonly GetMyInvoicesHandler sut;

    public GetMyInvoicesHandlerTests() => sut = new GetMyInvoicesHandler(dbFactory);

    public void Dispose() => dbFactory.Dispose();

    [Fact]
    public async Task Returns_only_the_requesting_users_invoices_ordered_by_generation_date_descending()
    {
        int meId, otherId;
        await using (var db = dbFactory.CreateDbContext())
        {
            var period1 = new OrderingPeriod { Name = "Augusztus", StartDate = new DateOnly(2026, 8, 1), EndDate = new DateOnly(2026, 8, 31), OrderDeadline = new DateTime(2026, 7, 15, 10, 0, 0) };
            var period2 = new OrderingPeriod { Name = "Szeptember", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 9, 30), OrderDeadline = new DateTime(2026, 8, 15, 10, 0, 0) };
            db.OrderingPeriods.AddRange(period1, period2);

            var role = new Role { Name = "User" };
            db.Roles.Add(role);
            await db.SaveChangesAsync();

            var me = new User { UserId = 1, UserName = "me", RoleId = role.Id };
            var other = new User { UserId = 2, UserName = "other", RoleId = role.Id };
            db.Users.AddRange(me, other);
            await db.SaveChangesAsync();
            meId = me.Id;
            otherId = other.Id;

            db.PeriodInvoices.AddRange(
                new PeriodInvoice
                {
                    UserId = meId, OrderingPeriodId = period1.Id, SequenceNumber = 1, GrossHuf = 1400,
                    CreditAppliedHuf = 0, PayableHuf = 1400,
                    GeneratedAtUtc = new DateTime(2026, 7, 20, 9, 0, 0),
                },
                new PeriodInvoice
                {
                    UserId = meId, OrderingPeriodId = period2.Id, SequenceNumber = 1, GrossHuf = 2800,
                    CreditAppliedHuf = 1400, PayableHuf = 1400,
                    GeneratedAtUtc = new DateTime(2026, 8, 20, 9, 0, 0),
                },
                new PeriodInvoice
                {
                    UserId = otherId, OrderingPeriodId = period2.Id, SequenceNumber = 1, GrossHuf = 1400,
                    CreditAppliedHuf = 0, PayableHuf = 1400,
                    GeneratedAtUtc = new DateTime(2026, 8, 20, 9, 0, 0),
                });
            await db.SaveChangesAsync();
        }

        var result = await sut.Handle(new GetMyInvoicesQuery(meId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);

        Assert.Equal("Szeptember", result.Value[0].PeriodName); // most recently generated first
        Assert.Equal("Augusztus", result.Value[1].PeriodName);
        Assert.Equal(1400, result.Value[0].CreditAppliedHuf);
    }
}
