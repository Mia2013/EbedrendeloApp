using EbedrendeloApp.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.Billing.GetInvoices;

public sealed class GetInvoicesHandler(IDbContextFactory<EbedrendeloDbContext> dbFactory)
    : IRequestHandler<GetInvoicesQuery, IReadOnlyList<InvoiceDto>>
{
    public async Task<IReadOnlyList<InvoiceDto>> Handle(GetInvoicesQuery request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var query = db.PeriodInvoices.AsQueryable();
        if (request.OrderingPeriodId is { } periodId)
        {
            query = query.Where(i => i.OrderingPeriodId == periodId);
        }

        if (request.IsPaid is { } isPaid)
        {
            query = query.Where(i => i.IsPaid == isPaid);
        }

        var invoices = await query
            .OrderByDescending(i => i.GeneratedAtUtc).ThenByDescending(i => i.SequenceNumber)
            .ToListAsync(cancellationToken);

        var userIds = invoices.Select(i => i.UserId).Distinct().ToList();
        var userNames = await db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => $"{u.VezetekNev} {u.KeresztNev}".Trim(), cancellationToken);

        var periodIds = invoices.Select(i => i.OrderingPeriodId).Distinct().ToList();
        var periodNames = await db.OrderingPeriods
            .Where(p => periodIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        // Hány menünap tartozik az egyes számlákhoz — egy csoportosított lekérdezés, nem számlánként egy.
        var invoiceIds = invoices.Select(i => i.Id).ToList();
        var dayCounts = await db.MenuOrders
            .Where(o => o.PeriodInvoiceId != null && invoiceIds.Contains(o.PeriodInvoiceId!.Value))
            .GroupBy(o => o.PeriodInvoiceId!.Value)
            .Select(g => new { InvoiceId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.InvoiceId, x => x.Count, cancellationToken);

        var result = invoices
            .Select(i => new InvoiceDto(
                i.Id,
                i.UserId,
                userNames.GetValueOrDefault(i.UserId, "Ismeretlen felhasználó"),
                i.OrderingPeriodId,
                periodNames.GetValueOrDefault(i.OrderingPeriodId, "Ismeretlen időszak"),
                i.SequenceNumber,
                dayCounts.GetValueOrDefault(i.Id, 0),
                i.GrossHuf,
                i.CreditAppliedHuf,
                i.PayableHuf,
                i.IsPaid,
                i.PaidAtUtc,
                i.GeneratedAtUtc))
            .ToList();

        return result;
    }
}
