using System.Data;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Services;
using EbedrendeloApp.Common.Time;
using EbedrendeloApp.Data;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.Billing.GeneratePeriodInvoices;

public sealed class GeneratePeriodInvoicesHandler(
    IDbContextFactory<EbedrendeloDbContext> dbFactory,
    IAppClock clock,
    ICreditService creditService,
    INotificationService notificationService,
    ILogger<GeneratePeriodInvoicesHandler> logger)
    : IRequestHandler<GeneratePeriodInvoicesCommand, Result<BatchInvoiceResult>>
{
    public async Task<Result<BatchInvoiceResult>> Handle(GeneratePeriodInvoicesCommand request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var period = await db.OrderingPeriods.FirstOrDefaultAsync(p => p.Id == request.OrderingPeriodId, cancellationToken);
        if (period is null)
        {
            return Result.Failure<BatchInvoiceResult>(ErrorCodes.NotFound, "Az időszak nem található.");
        }

        // period.IsOpen nem jó kapu ide — az a hónap közbeni napi lemondást/pótrendelést is szabályozza,
        // és annak végig működnie kell. Az OrderDeadline a tömeges leadási ablak zárása, IsOpen-től
        // függetlenül.
        if (clock.LocalNow <= period.OrderDeadline)
        {
            return Result.Failure<BatchInvoiceResult>(ErrorCodes.OrderWindowOpen, "A rendelési határidő még nem telt le, korai a számlázás.");
        }

        // Serializable — két párhuzamos futás ne számolja ki ugyanazt a következő sorszámot, és ne
        // számlázza ki kétszer ugyanazt a napot.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        // Delta-számlázás: csak az még ki nem számlázott aktív napok. Entitásként kell, mert a mentés
        // után rájuk írjuk a számla id-ját.
        var uninvoicedOrders = await db.MenuOrders
            .Where(o => o.OrderingPeriodId == request.OrderingPeriodId
                        && o.Status == OrderStatus.Active
                        && o.PeriodInvoiceId == null)
            .ToListAsync(cancellationToken);

        if (uninvoicedOrders.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return Result.Success(new BatchInvoiceResult([]));
        }

        var ordersByUser = uninvoicedOrders
            .GroupBy(o => o.UserId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<MenuOrder>)g.ToList());
        var userIds = ordersByUser.Keys.ToList();

        // A már kiállított számlák legnagyobb sorszáma felhasználónként — ehhez képest +1 a mostani.
        var lastSequenceByUser = await db.PeriodInvoices
            .Where(i => i.OrderingPeriodId == request.OrderingPeriodId && userIds.Contains(i.UserId))
            .GroupBy(i => i.UserId)
            .Select(g => new { UserId = g.Key, Max = g.Max(i => i.SequenceNumber) })
            .ToDictionaryAsync(x => x.UserId, x => x.Max, cancellationToken);

        // FIFO (3.3) — a legrégebbi jóváírás fogy először; a frissen keletkezett is azonnal beszámítható
        // (nincs EligibleFrom), ezért a RemainingHuf > 0 szűrésen túl nincs dátumfeltétel.
        var availableCredits = await db.CreditEntries
            .Where(c => userIds.Contains(c.UserId) && c.RemainingHuf > 0)
            .OrderBy(c => c.CreatedAtUtc).ThenBy(c => c.Id)
            .ToListAsync(cancellationToken);
        var creditsByUser = availableCredits
            .GroupBy(c => c.UserId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<CreditEntry>)g.ToList());

        var nowUtc = clock.UtcNow.UtcDateTime;
        var invoicesByUser = new Dictionary<int, PeriodInvoice>();
        var appliedEntriesByUser = new Dictionary<int, IReadOnlyList<CreditEntry>>();

        foreach (var userId in userIds)
        {
            var orders = ordersByUser[userId];
            var grossHuf = orders.Sum(o => o.PriceHuf);

            var applied = creditService.ApplyCreditToInvoice(
                db, creditsByUser.GetValueOrDefault(userId, []), grossHuf, request.GeneratedByUserId, nowUtc);

            var invoice = new PeriodInvoice
            {
                UserId = userId,
                OrderingPeriodId = request.OrderingPeriodId,
                SequenceNumber = lastSequenceByUser.GetValueOrDefault(userId, 0) + 1,
                GrossHuf = grossHuf,
                CreditAppliedHuf = applied.TotalAppliedHuf,
                PayableHuf = grossHuf - applied.TotalAppliedHuf,
                GeneratedAtUtc = nowUtc,
            };
            db.PeriodInvoices.Add(invoice);

            invoicesByUser[userId] = invoice;
            appliedEntriesByUser[userId] = applied.AppliedEntries;
        }

        // A PeriodInvoice.Id csak az első mentés után ismert, a rendelésekre és a CreditApplied
        // tételekre viszont rá kell írni — ezért két fázis (mint a CloseDayHandler-nél).
        await db.SaveChangesAsync(cancellationToken);

        foreach (var (userId, invoice) in invoicesByUser)
        {
            foreach (var order in ordersByUser[userId])
            {
                order.PeriodInvoiceId = invoice.Id;
            }

            foreach (var entry in appliedEntriesByUser[userId])
            {
                entry.PeriodInvoiceId = invoice.Id;
            }

            if (invoice.CreditAppliedHuf > 0)
            {
                // AC 7.1.5 kifejezetten a levont jóváírás részletezéséhez köti az értesítést — 0 Ft
                // beszámításnál nincs külön "számla kész" üzenet (nincs is rá NotificationType).
                notificationService.Notify(
                    db,
                    userId,
                    NotificationType.CreditApplied,
                    invoice.SequenceNumber == 1 ? "Számla elkészült" : "Kiegészítő számla elkészült",
                    $"A(z) {period.Name} időszak {(invoice.SequenceNumber == 1 ? "számlája" : $"{invoice.SequenceNumber}. számlája")} elkészült. " +
                    $"Beszámított jóváírás: {invoice.CreditAppliedHuf} Ft. Fizetendő: {invoice.PayableHuf} Ft.",
                    nowUtc);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Számlagenerálás kész: időszak {PeriodId} ({PeriodName}), {InvoiceCount} számla, {DayCount} nap, " +
            "{PayableHuf} Ft fizetendő, {CreditHuf} Ft beszámított jóváírás; generálta: {GeneratedByUserId}",
            request.OrderingPeriodId, period.Name, invoicesByUser.Count, uninvoicedOrders.Count,
            invoicesByUser.Values.Sum(i => i.PayableHuf), invoicesByUser.Values.Sum(i => i.CreditAppliedHuf),
            request.GeneratedByUserId);

        var generated = invoicesByUser
            .Select(pair => new GeneratedInvoiceDto(
                pair.Value.Id,
                pair.Key,
                pair.Value.SequenceNumber,
                ordersByUser[pair.Key].Count,
                pair.Value.GrossHuf,
                pair.Value.CreditAppliedHuf,
                pair.Value.PayableHuf))
            .ToList();

        return Result.Success(new BatchInvoiceResult(generated));
    }
}
