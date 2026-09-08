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
    INotificationService notificationService)
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

        // period.IsOpen is not the right gate here — it also controls in-month day-by-day
        // cancellation/ordering (3.1) and must stay usable through the eating period. OrderDeadline
        // marks the close of the initial bulk-ordering window, independent of IsOpen.
        if (clock.LocalNow <= period.OrderDeadline)
        {
            return Result.Failure<BatchInvoiceResult>(ErrorCodes.OrderWindowOpen, "A rendelési határidő még nem telt le, korai a számlázás.");
        }

        // Serializable, mirroring CloseDayHandler — guards against two concurrent runs generating a
        // duplicate invoice for the same user+period before either has committed.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var menuUserIds = db.MenuOrders
            .Where(o => o.OrderingPeriodId == request.OrderingPeriodId && o.Status == OrderStatus.Active)
            .Select(o => o.UserId);
        var alaCarteUserIds = db.ALaCarteOrders
            .Where(o => o.OrderingPeriodId == request.OrderingPeriodId)
            .Select(o => o.UserId);
        var relevantUserIds = await menuUserIds.Union(alaCarteUserIds).ToListAsync(cancellationToken);

        if (relevantUserIds.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return Result.Success(new BatchInvoiceResult([], []));
        }

        var invoicedUserPeriods = await PeriodInvoiceQueries.GetInvoicedUserPeriodsAsync(
            db, relevantUserIds, [request.OrderingPeriodId], cancellationToken);

        var skippedUserIds = relevantUserIds.Where(userId => invoicedUserPeriods.Contains((userId, request.OrderingPeriodId))).ToList();
        var toGenerate = relevantUserIds.Except(skippedUserIds).ToList();

        if (toGenerate.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return Result.Success(new BatchInvoiceResult([], skippedUserIds));
        }

        // AC 7.1.1/7.1.2 — batch-summed rather than per-user queries, to avoid an N+1 over toGenerate.
        var menuGrossByUser = await db.MenuOrders
            .Where(o => o.OrderingPeriodId == request.OrderingPeriodId && o.Status == OrderStatus.Active && toGenerate.Contains(o.UserId))
            .GroupBy(o => o.UserId)
            .Select(g => new { UserId = g.Key, Sum = g.Sum(o => o.PriceHuf) })
            .ToDictionaryAsync(x => x.UserId, x => x.Sum, cancellationToken);

        var alaCarteGrossByUser = await db.ALaCarteOrders
            .Where(o => o.OrderingPeriodId == request.OrderingPeriodId && toGenerate.Contains(o.UserId))
            .GroupBy(o => o.UserId)
            .Select(g => new { UserId = g.Key, Sum = g.Sum(o => o.TotalHuf) })
            .ToDictionaryAsync(x => x.UserId, x => x.Sum, cancellationToken);

        // FIFO order (3.3) — oldest CreatedAtUtc consumed first; a freshly issued credit is immediately
        // eligible (no EligibleFrom), so no extra date filter beyond RemainingHuf > 0 is needed.
        var availableCredits = await db.CreditEntries
            .Where(c => toGenerate.Contains(c.UserId) && c.RemainingHuf > 0)
            .OrderBy(c => c.CreatedAtUtc).ThenBy(c => c.Id)
            .ToListAsync(cancellationToken);
        var creditsByUser = availableCredits
            .GroupBy(c => c.UserId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<CreditEntry>)g.ToList());

        var nowUtc = clock.UtcNow.UtcDateTime;
        var invoicesByUser = new Dictionary<int, PeriodInvoice>();
        var appliedEntriesByUser = new Dictionary<int, IReadOnlyList<CreditEntry>>();

        foreach (var userId in toGenerate)
        {
            var menuGross = menuGrossByUser.GetValueOrDefault(userId, 0);
            var alaCarteGross = alaCarteGrossByUser.GetValueOrDefault(userId, 0);

            var applied = creditService.ApplyCreditToInvoice(
                db, creditsByUser.GetValueOrDefault(userId, []), menuGross, request.GeneratedByUserId, nowUtc);

            var invoice = new PeriodInvoice
            {
                UserId = userId,
                OrderingPeriodId = request.OrderingPeriodId,
                MenuGrossHuf = menuGross,
                ALaCarteGrossHuf = alaCarteGross,
                GrossHuf = menuGross + alaCarteGross,
                CreditAppliedHuf = applied.TotalAppliedHuf,
                MenuPayableHuf = menuGross - applied.TotalAppliedHuf,
                ALaCartePayableHuf = alaCarteGross,
                PayableHuf = menuGross - applied.TotalAppliedHuf + alaCarteGross,
                GeneratedAtUtc = nowUtc,
            };
            db.PeriodInvoices.Add(invoice);

            invoicesByUser[userId] = invoice;
            appliedEntriesByUser[userId] = applied.AppliedEntries;
        }

        // PeriodInvoice.Id is required on CreditEntry.PeriodInvoiceId — only known after this first save,
        // so the CreditApplied entries are stamped in a second phase (mirrors CloseDayHandler's
        // KitchenClosureLine sequencing).
        await db.SaveChangesAsync(cancellationToken);

        foreach (var (userId, entries) in appliedEntriesByUser)
        {
            var invoiceId = invoicesByUser[userId].Id;
            foreach (var entry in entries)
            {
                entry.PeriodInvoiceId = invoiceId;
            }
        }

        foreach (var invoice in invoicesByUser.Values)
        {
            if (invoice.CreditAppliedHuf > 0)
            {
                // AC 7.1.5 ties the notification to the deducted-credit breakdown specifically — a
                // 0-Ft-applied invoice has no NotificationType of its own for a plain "ready" message.
                notificationService.Notify(
                    db,
                    invoice.UserId,
                    NotificationType.CreditApplied,
                    "Számla elkészült",
                    $"A(z) {period.Name} időszak számlája elkészült. Beszámított jóváírás: {invoice.CreditAppliedHuf} Ft. " +
                    $"Fizetendő: {invoice.PayableHuf} Ft (menü: {invoice.MenuPayableHuf} Ft, à la carte: {invoice.ALaCartePayableHuf} Ft).",
                    nowUtc);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var generated = invoicesByUser.Values
            .Select(i => new GeneratedInvoiceDto(
                i.Id, i.UserId, i.MenuGrossHuf, i.ALaCarteGrossHuf, i.CreditAppliedHuf, i.MenuPayableHuf, i.ALaCartePayableHuf, i.PayableHuf))
            .ToList();

        return Result.Success(new BatchInvoiceResult(generated, skippedUserIds));
    }
}
