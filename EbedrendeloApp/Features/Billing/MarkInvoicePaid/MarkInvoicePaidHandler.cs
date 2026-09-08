using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Time;
using EbedrendeloApp.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.Billing.MarkInvoicePaid;

public sealed class MarkInvoicePaidHandler(
    IDbContextFactory<EbedrendeloDbContext> dbFactory,
    IAppClock clock,
    ILogger<MarkInvoicePaidHandler> logger)
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

        logger.LogInformation(
            "Számla fizetettnek jelölve: {InvoiceId} (dolgozó {UserId}, {PayableHuf} Ft); jelölte: {MarkedByUserId}",
            invoice.Id, invoice.UserId, invoice.PayableHuf, request.MarkedByUserId);

        return Result.Success();
    }
}
