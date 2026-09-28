using EbedrendeloApp.Data.Seed;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Tests.Data.Seed;

/// <summary>
/// A seed nem „csak demoadat": ugyanabba a ledgerbe és ugyanazokra a számlákra ír, amiket az éles
/// kódútvonalak olvasnak. Egy hibás seed-sor ezért valódi pénzügyi hibaként jelenik meg az első
/// számlagenerálásnál — pontosan ez történt, amikor a Fázis 1 „jóváírás csak kiszámlázott napért"
/// szabálya után a seed feltétel nélkül gyártotta tovább a jóváírásokat.
/// </summary>
public class DatabaseSeederTests : IDisposable
{
    private readonly SqliteDbContextFactory dbFactory = new();

    public void Dispose() => dbFactory.Dispose();

    [Fact]
    public async Task Never_issues_a_cancellation_credit_for_an_order_that_was_never_invoiced()
    {
        await using var db = dbFactory.CreateDbContext();
        await DatabaseSeeder.SeedAsync(db);

        var creditedOrderIds = await db.CreditEntries
            .Where(c => c.Kind == CreditEntryKind.CancellationCredit && c.SourceMenuOrderId != null)
            .Select(c => c.SourceMenuOrderId!.Value)
            .ToListAsync();

        var uninvoiced = await db.MenuOrders
            .Where(o => creditedOrderIds.Contains(o.Id) && o.PeriodInvoiceId == null)
            .Select(o => o.Date)
            .ToListAsync();

        Assert.True(
            uninvoiced.Count == 0,
            $"Jóváírás keletkezett ki nem számlázott napokra: {string.Join(", ", uninvoiced)}");
    }

    [Fact]
    public async Task Invoices_the_closed_previous_period_so_the_ledger_has_real_entries()
    {
        await using var db = dbFactory.CreateDbContext();
        await DatabaseSeeder.SeedAsync(db);

        var closedPeriod = await db.OrderingPeriods.OrderBy(p => p.StartDate).FirstAsync();
        var invoices = await db.PeriodInvoices.Where(i => i.OrderingPeriodId == closedPeriod.Id).ToListAsync();

        Assert.NotEmpty(invoices);
        Assert.All(invoices, i => Assert.Equal(1, i.SequenceNumber));
        Assert.All(invoices, i => Assert.Equal(i.GrossHuf - i.CreditAppliedHuf, i.PayableHuf));

        // A számlázott napokért viszont JÁR jóváírás — enélkül az egyenleg-képernyők üresek lennének.
        Assert.NotEmpty(await db.CreditEntries.Where(c => c.Kind == CreditEntryKind.CancellationCredit).ToListAsync());
    }

    [Fact]
    public async Task Leaves_the_current_and_next_periods_uninvoiced_so_generation_can_be_demonstrated()
    {
        await using var db = dbFactory.CreateDbContext();
        await DatabaseSeeder.SeedAsync(db);

        var periods = await db.OrderingPeriods.OrderBy(p => p.StartDate).ToListAsync();
        var laterPeriodIds = periods.Skip(1).Select(p => p.Id).ToList();

        Assert.Empty(await db.PeriodInvoices.Where(i => laterPeriodIds.Contains(i.OrderingPeriodId)).ToListAsync());
    }

    [Fact]
    public async Task Is_idempotent_when_run_twice()
    {
        await using var db = dbFactory.CreateDbContext();
        await DatabaseSeeder.SeedAsync(db);
        var afterFirst = (
            Users: await db.Users.CountAsync(),
            Orders: await db.MenuOrders.CountAsync(),
            Invoices: await db.PeriodInvoices.CountAsync(),
            Credits: await db.CreditEntries.CountAsync());

        await DatabaseSeeder.SeedAsync(db);

        Assert.Equal(afterFirst.Users, await db.Users.CountAsync());
        Assert.Equal(afterFirst.Orders, await db.MenuOrders.CountAsync());
        Assert.Equal(afterFirst.Invoices, await db.PeriodInvoices.CountAsync());
        Assert.Equal(afterFirst.Credits, await db.CreditEntries.CountAsync());
    }
}
