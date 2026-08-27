using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Services;
using EbedrendeloApp.Common.Time;
using EbedrendeloApp.Data;
using EbedrendeloApp.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.Calendar.RemoveExcludedDay;

public sealed class RemoveExcludedDayHandler(
    IDbContextFactory<EbedrendeloDbContext> dbFactory,
    IAppClock clock,
    ICreditService creditService,
    INotificationService notificationService)
    : IRequestHandler<RemoveExcludedDayCommand, Result<RemoveExcludedDayResult>>
{
    public async Task<Result<RemoveExcludedDayResult>> Handle(RemoveExcludedDayCommand request, CancellationToken cancellationToken)
    {
        if (request.Date <= clock.Today)
        {
            return Result.Failure<RemoveExcludedDayResult>(ErrorCodes.NotFutureDate, "Csak jövőbeli nap kizárása vonható vissza.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var excluded = await db.ExcludedDays.FirstOrDefaultAsync(e => e.Date == request.Date, cancellationToken);
        if (excluded is null)
        {
            return Result.Failure<RemoveExcludedDayResult>(ErrorCodes.NotFound, "Ez a nap nincs kizárva.");
        }

        if (await db.KitchenClosures.AnyAsync(k => k.Date == request.Date, cancellationToken))
        {
            return Result.Failure<RemoveExcludedDayResult>(ErrorCodes.DayClosed, "A nap már le van zárva.");
        }

        db.ExcludedDays.Remove(excluded);

        if (!request.RestoreCancelledOrders)
        {
            // The FK on MenuOrder.CancelledByExcludedDayId is Restrict — any order still pointing at
            // this ExcludedDay must be detached before the row is deleted. CancellationReason stays
            // DayExcluded on these orders as the historical record.
            await db.MenuOrders
                .Where(o => o.CancelledByExcludedDayId == excluded.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.CancelledByExcludedDayId, (int?)null), cancellationToken);

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success(new RemoveExcludedDayResult(0, 0, []));
        }

        var nowUtc = clock.UtcNow.UtcDateTime;
        var candidates = await db.MenuOrders
            .Where(o => o.CancelledByExcludedDayId == excluded.Id)
            .ToListAsync(cancellationToken);

        // TODO: at very large daily order volumes (a few thousand+ candidates for one excluded day)
        // these .Contains() lists could approach SQL Server's ~2100-parameter-per-query ceiling. At
        // today's expected scale (~1000 orders/day) this stays comfortably under it; if that grows
        // materially, switch to a table-valued parameter / temp-table join instead of inline IN lists.
        var orderIds = candidates.Select(o => o.Id).ToList();
        var userIds = candidates.Select(o => o.UserId).Distinct().ToList();
        var periodIds = candidates.Select(o => o.OrderingPeriodId).Distinct().ToList();
        var candidateDates = candidates.Select(o => o.Date).Distinct().ToList();

        var userNames = await db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => $"{u.VezetekNev} {u.KeresztNev}".Trim(), cancellationToken);

        // Batch-load everything the per-order checks below need, instead of 3 queries per candidate
        // order (was an N+1 pattern — see 01-szerver-architektura.md discussion on this handler).
        // The "pick the oldest entry per order" grouping is done client-side (not
        // .GroupBy(...).Select(g => g.First()) on the IQueryable) on purpose: SQL's GROUP BY doesn't
        // guarantee row order within a group, so a server-translated GroupBy+First has no reliable way
        // to honor "oldest wins" — this materializes first, then groups with plain, guaranteed
        // LINQ-to-Objects semantics. The row count here is bounded by one excluded day's candidate
        // orders, so this stays cheap even at ~1000 rows.
        var creditEntries = await db.CreditEntries
            .Where(c => c.SourceMenuOrderId != null && orderIds.Contains(c.SourceMenuOrderId.Value) && c.Kind == CreditEntryKind.CancellationCredit)
            .ToListAsync(cancellationToken);
        var creditEntryByOrderId = creditEntries
            .GroupBy(c => c.SourceMenuOrderId!.Value)
            .ToDictionary(g => g.Key, g =>
            {
                // An order can go through more than one exclude/restore cycle: ExcludeDayHandler always
                // inserts a brand-new CreditEntry rather than reusing one, and CreditService.RevokeCredit
                // only zeroes RemainingHuf on restore — it never removes the row. So the oldest entry for
                // an order can be a stale, already-revoked one from an earlier cycle. Prefer the still-live
                // entry (RemainingHuf == AmountHuf) for THIS cycle; if none is live (most recent takes
                // priority in case of a data anomaly), fall back to the most recent one so the existing
                // "already consumed" skip path below still behaves correctly.
                return g.Where(c => c.RemainingHuf == c.AmountHuf).OrderByDescending(c => c.Id).FirstOrDefault()
                    ?? g.OrderByDescending(c => c.Id).First();
            });

        var invoicedUserPeriods = (await db.PeriodInvoices
            .Where(i => userIds.Contains(i.UserId) && periodIds.Contains(i.OrderingPeriodId))
            .Select(i => new { i.UserId, i.OrderingPeriodId })
            .ToListAsync(cancellationToken))
            .Select(i => (i.UserId, i.OrderingPeriodId))
            .ToHashSet();

        var activeOrdersOnCandidateDates = await db.MenuOrders
            .Where(o => o.Status == OrderStatus.Active && userIds.Contains(o.UserId) && candidateDates.Contains(o.Date))
            .Select(o => new { o.Id, o.UserId, o.Date })
            .ToListAsync(cancellationToken);

        var restoredCount = 0;
        var skipped = new List<SkippedOrderInfo>();

        foreach (var order in candidates)
        {
            var creditEntry = creditEntryByOrderId.GetValueOrDefault(order.Id);

            var hasInvoice = invoicedUserPeriods.Contains((order.UserId, order.OrderingPeriodId));

            var hasNewerActiveOrder = activeOrdersOnCandidateDates
                .Any(a => a.Id != order.Id && a.UserId == order.UserId && a.Date == order.Date);

            var userName = userNames.GetValueOrDefault(order.UserId, "Ismeretlen felhasználó");

            string? skipReason = null;
            if (creditEntry is null || creditEntry.RemainingHuf != creditEntry.AmountHuf)
            {
                skipReason = "a jóváírása időközben felhasználásra került, nem vonható vissza automatikusan";
            }
            else if (hasInvoice)
            {
                skipReason = "erre az időszakra már készült számla, a jóváírása nem vonható vissza automatikusan";
            }
            else if (hasNewerActiveOrder)
            {
                skipReason = "időközben új aktív rendelése keletkezett erre a napra";
            }

            if (skipReason is null)
            {
                order.Status = OrderStatus.Active;
                order.CancelledAtUtc = null;
                order.CancelledByUserId = null;
                order.CancellationReason = null;
                order.CancelledByExcludedDayId = null;

                creditService.RevokeCredit(db, creditEntry!, request.PerformedByUserId, nowUtc, "Kizárás visszavonva");
                notificationService.Notify(
                    db,
                    order.UserId,
                    NotificationType.OrderRestored,
                    "Rendelésed helyreállt",
                    $"A(z) {request.Date:yyyy.MM.dd} nap kizárását visszavonták, a rendelésed újra aktív.",
                    nowUtc,
                    request.Date,
                    order.Id);

                restoredCount++;
            }
            else
            {
                // Detach from the ExcludedDay that is about to be deleted (Restrict FK) — the order
                // stays Cancelled with CancellationReason = DayExcluded as the historical record.
                order.CancelledByExcludedDayId = null;

                skipped.Add(new SkippedOrderInfo(userName, skipReason));
                notificationService.Notify(
                    db,
                    order.UserId,
                    NotificationType.DayReopened,
                    "A nap újranyitva",
                    $"A(z) {request.Date:yyyy.MM.dd} nap mégis kiszolgálásra kerül, a jóváírásod megmarad, a leadási határidőn belül újra rendelhetsz.",
                    nowUtc,
                    request.Date,
                    order.Id);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success(new RemoveExcludedDayResult(restoredCount, skipped.Count, skipped));
    }
}
