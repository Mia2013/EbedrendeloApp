using EbedrendeloApp.Common.Calendar;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Services;
using EbedrendeloApp.Common.Time;
using EbedrendeloApp.Data;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.Orders.CancelMenuOrders;

public sealed class CancelMenuOrdersHandler(
    IDbContextFactory<EbedrendeloDbContext> dbFactory,
    IAppClock clock,
    IWorkingDayCalculator workingDayCalculator,
    ICreditService creditService,
    INotificationService notificationService)
    : IRequestHandler<CancelMenuOrdersCommand, Result<BatchOrderResult>>
{
    public async Task<Result<BatchOrderResult>> Handle(CancelMenuOrdersCommand request, CancellationToken cancellationToken)
    {
        var dates = request.Dates.Distinct().ToList();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var activeOrders = await db.MenuOrders
            .Where(o => o.UserId == request.TargetUserId && o.Status == OrderStatus.Active && dates.Contains(o.Date))
            .ToDictionaryAsync(o => o.Date, cancellationToken);

        var variantIds = activeOrders.Values.Select(o => o.MenuVariantId).Distinct().ToList();
        var variantCodes = await db.MenuVariants
            .Where(v => variantIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, v => v.Code, cancellationToken);

        var periodIds = activeOrders.Values.Select(o => o.OrderingPeriodId).Distinct().ToList();
        var periods = await db.OrderingPeriods
            .Where(p => periodIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var settings = await db.AppSettings.FirstAsync(cancellationToken);
        var excludedDates = await db.ExcludedDays.Select(e => e.Date).ToHashSetAsync(cancellationToken);
        var kitchenClosures = await db.KitchenClosures.Select(k => k.Date).ToHashSetAsync(cancellationToken);

        var nowLocal = clock.LocalNow;
        var nowUtc = clock.UtcNow.UtcDateTime;

        var succeeded = new List<DayResult>();
        var skipped = new List<DaySkip>();

        foreach (var date in dates)
        {
            // Explicit guard for AC 3.2.2 (aznapi lemondás szigorúan tilos) — otherwise this is only an
            // emergent property of ChangeDeadlineWorkingDays always being >= 1, which nothing enforces.
            if (date <= clock.Today)
            {
                skipped.Add(new DaySkip(date, ErrorCodes.DeadlinePassed));
                continue;
            }

            if (!activeOrders.TryGetValue(date, out var order))
            {
                skipped.Add(new DaySkip(date, ErrorCodes.NoActiveOrder));
                continue;
            }

            if (kitchenClosures.Contains(date))
            {
                skipped.Add(new DaySkip(date, ErrorCodes.DayClosed));
                continue;
            }

            var period = periods[order.OrderingPeriodId];

            // Cancellation also requires the period to still be IsOpen, matching the already-shipped
            // GetOrderableDaysHandler (not just the literal 3.5 table row for lemondás) — see the plan's
            // "tervezési döntések" for why command and query must agree here (AC 1.7.4).
            if (!period.IsOpen)
            {
                skipped.Add(new DaySkip(date, ErrorCodes.PeriodClosed));
                continue;
            }

            if (!workingDayCalculator.CanChange(date, nowLocal, settings, excludedDates, hasKitchenClosure: false))
            {
                skipped.Add(new DaySkip(date, ErrorCodes.DeadlinePassed));
                continue;
            }

            order.Status = OrderStatus.Cancelled;
            order.CancelledAtUtc = nowUtc;
            order.CancelledByUserId = request.CancelledByUserId;
            order.CancellationReason = CancellationReason.ByUser;

            creditService.IssueCancellationCredit(db, order, request.CancelledByUserId, nowUtc);
            notificationService.Notify(
                db,
                order.UserId,
                NotificationType.CreditIssued,
                "Rendelésed lemondva",
                $"A(z) {date:yyyy.MM.dd} napi rendelésed lemondásra került, az összeg jóváírásra került.",
                nowUtc,
                date,
                order.Id);

            succeeded.Add(new DayResult(date, variantCodes[order.MenuVariantId]));
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success(new BatchOrderResult(succeeded, skipped));
    }
}
