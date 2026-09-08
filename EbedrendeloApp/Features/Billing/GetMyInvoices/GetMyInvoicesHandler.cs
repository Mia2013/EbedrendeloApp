using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.Billing.GetMyInvoices;

public sealed class GetMyInvoicesHandler(IDbContextFactory<EbedrendeloDbContext> dbFactory)
    : IRequestHandler<GetMyInvoicesQuery, Result<IReadOnlyList<MyInvoiceDto>>>
{
    public async Task<Result<IReadOnlyList<MyInvoiceDto>>> Handle(GetMyInvoicesQuery request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var invoices = await db.PeriodInvoices
            .Where(i => i.UserId == request.UserId)
            .OrderByDescending(i => i.GeneratedAtUtc)
            .ToListAsync(cancellationToken);

        var periodIds = invoices.Select(i => i.OrderingPeriodId).Distinct().ToList();
        var periods = await db.OrderingPeriods
            .Where(p => periodIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

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
                    i.MenuGrossHuf,
                    i.ALaCarteGrossHuf,
                    i.GrossHuf,
                    i.CreditAppliedHuf,
                    i.MenuPayableHuf,
                    i.ALaCartePayableHuf,
                    i.PayableHuf,
                    i.IsPaid,
                    i.PaidAtUtc,
                    i.GeneratedAtUtc);
            })
            .ToList();

        return Result.Success<IReadOnlyList<MyInvoiceDto>>(result);
    }
}
