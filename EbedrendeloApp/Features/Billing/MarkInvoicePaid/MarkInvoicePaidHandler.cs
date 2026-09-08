using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Time;
using EbedrendeloApp.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.Billing.MarkInvoicePaid;

public sealed class MarkInvoicePaidHandler(IDbContextFactory<EbedrendeloDbContext> dbFactory, IAppClock clock)
    : IRequestHandler<MarkInvoicePaidCommand, Result>
{
    public async Task<Result> Handle(MarkInvoicePaidCommand request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var invoice = await db.PeriodInvoices.FirstOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken);
        if (invoice is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "A számla nem található.");
        }

        if (invoice.IsPaid)
        {
            return Result.Failure(ErrorCodes.AlreadyPaid, "A számla már ki van fizetve jelölve.");
        }

        invoice.IsPaid = true;
        invoice.PaidAtUtc = clock.UtcNow.UtcDateTime;
        invoice.MarkedPaidByUserId = request.MarkedByUserId;

        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
