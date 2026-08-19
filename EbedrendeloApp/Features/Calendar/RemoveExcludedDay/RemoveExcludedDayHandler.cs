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

        var userIds = candidates.Select(o => o.UserId).Distinct().ToList();
        var userNames = await db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => $"{u.VezetekNev} {u.KeresztNev}".Trim(), cancellationToken);

        var restoredCount = 0;
        var skipped = new List<SkippedOrderInfo>();

        foreach (var order in candidates)
        {
            var creditEntry = await db.CreditEntries
                .Where(c => c.SourceMenuOrderId == order.Id && c.Kind == CreditEntryKind.CancellationCredit)
                .FirstOrDefaultAsync(cancellationToken);

            var hasInvoice = await db.PeriodInvoices
                .AnyAsync(i => i.UserId == order.UserId && i.OrderingPeriodId == order.OrderingPeriodId, cancellationToken);

            var hasNewerActiveOrder = await db.MenuOrders
                .AnyAsync(o => o.Id != order.Id && o.UserId == order.UserId && o.Date == order.Date && o.Status == OrderStatus.Active, cancellationToken);

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
