using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Features.Billing.MarkInvoicePaid;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Tests.Features.Billing;

public class MarkInvoicePaidHandlerTests : IDisposable
{
    private readonly SqliteDbContextFactory dbFactory = new();
    private readonly FixedAppClock clock = new(new DateTime(2026, 9, 10, 9, 0, 0));
    private readonly MarkInvoicePaidHandler sut;

    private int adminId;

    public MarkInvoicePaidHandlerTests() => sut = new MarkInvoicePaidHandler(dbFactory, clock);

    public void Dispose() => dbFactory.Dispose();

    private async Task<int> SeedInvoiceAsync(bool isPaid = false)
    {
        await using var db = dbFactory.CreateDbContext();

        var period = new OrderingPeriod
        {
            Name = "Szeptember",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 9, 30),
            OrderDeadline = new DateTime(2026, 8, 15, 10, 0, 0),
        };
        db.OrderingPeriods.Add(period);

        var role = new Role { Name = "User" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var admin = new User { UserId = 999, UserName = "admin", RoleId = role.Id };
        var worker = new User { UserId = 1, UserName = "worker", RoleId = role.Id };
        db.Users.AddRange(admin, worker);
        await db.SaveChangesAsync();
        adminId = admin.Id;

        var invoice = new PeriodInvoice
        {
            UserId = worker.Id,
            OrderingPeriodId = period.Id,
            SequenceNumber = 1,
            GrossHuf = 1400,
            CreditAppliedHuf = 0,
            PayableHuf = 1400,
            IsPaid = isPaid,
            PaidAtUtc = isPaid ? new DateTime(2026, 9, 1, 0, 0, 0) : null,
            MarkedPaidByUserId = isPaid ? admin.Id : null,
            GeneratedAtUtc = new DateTime(2026, 8, 20, 9, 0, 0),
        };
        db.PeriodInvoices.Add(invoice);
        await db.SaveChangesAsync();

        return invoice.Id;
    }

    [Fact]
    public async Task Marks_an_unpaid_invoice_as_paid()
    {
        var invoiceId = await SeedInvoiceAsync();

        var result = await sut.Handle(new MarkInvoicePaidCommand(invoiceId, adminId), CancellationToken.None);

        Assert.True(result.IsSuccess);

        await using var db = dbFactory.CreateDbContext();
        var invoice = await db.PeriodInvoices.SingleAsync(i => i.Id == invoiceId);
        Assert.True(invoice.IsPaid);
        Assert.Equal(clock.UtcNow.UtcDateTime, invoice.PaidAtUtc);
        Assert.Equal(adminId, invoice.MarkedPaidByUserId);
    }

    [Fact]
    public async Task Rejects_marking_an_already_paid_invoice_again()
    {
        var invoiceId = await SeedInvoiceAsync(isPaid: true);

        var result = await sut.Handle(new MarkInvoicePaidCommand(invoiceId, adminId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.AlreadyPaid, result.ErrorCode);
    }

    [Fact]
    public async Task Rejects_an_unknown_invoice_id()
    {
        await SeedInvoiceAsync();

        var result = await sut.Handle(new MarkInvoicePaidCommand(9999, adminId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NotFound, result.ErrorCode);
    }
}
