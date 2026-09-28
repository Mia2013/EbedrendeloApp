using EbedrendeloApp.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.Billing.GetMyInvoices;

public sealed class GetMyInvoicesHandler(IDbContextFactory<EbedrendeloDbContext> dbFactory)
    : IRequestHandler<GetMyInvoicesQuery, IReadOnlyList<MyInvoiceDto>>
{
    public async Task<IReadOnlyList<MyInvoiceDto>> Handle(GetMyInvoicesQuery request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var invoices = await db.PeriodInvoices
            .Where(i => i.UserId == request.UserId)
            .OrderByDescending(i => i.GeneratedAtUtc).ThenByDescending(i => i.SequenceNumber)
            .ToListAsync(cancellationToken);

        var periodIds = invoices.Select(i => i.OrderingPeriodId).Distinct().ToList();
        var periods = await db.OrderingPeriods
            .Where(p => periodIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var invoiceIds = invoices.Select(i => i.Id).ToList();
        var dayCounts = await db.MenuOrders
            .Where(o => o.PeriodInvoiceId != null && invoiceIds.Contains(o.PeriodInvoiceId!.Value))
            .GroupBy(o => o.PeriodInvoiceId!.Value)
            .Select(g => new { InvoiceId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.InvoiceId, x => x.Count, cancellationToken);

        var result = invoices
            .Select(i =>
            {
                var period = periods.GetValueOrDefault(i.OrderingPeriodId);
                return new MyInvoiceDto(
                    i.Id,
                    i.OrderingPeriodId,
                    period?.Name ?? "Ismeretlen időszak",
                    period?.StartDate ?? default,
                    period?.EndDate ?? default,
                    i.SequenceNumber,
                    dayCounts.GetValueOrDefault(i.Id, 0),
                    i.GrossHuf,
                    i.CreditAppliedHuf,
                    i.PayableHuf,
                    i.IsPaid,
                    i.PaidAtUtc,
                    i.GeneratedAtUtc);
            })
            .ToList();

        return result;
    }
}
