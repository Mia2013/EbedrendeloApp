using EbedrendeloApp.Common.Calendar;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Time;
using EbedrendeloApp.Data;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.Orders.PlacePeriodOrder;

public sealed class PlacePeriodOrderHandler(
    IDbContextFactory<EbedrendeloDbContext> dbFactory,
    IAppClock clock,
    IWorkingDayCalculator workingDayCalculator)
    : IRequestHandler<PlacePeriodOrderCommand, Result<BatchOrderResult>>
{
    private static readonly IReadOnlySet<DateOnly> ImmutableExcludedSet = new HashSet<DateOnly>();

    public async Task<Result<BatchOrderResult>> Handle(PlacePeriodOrderCommand request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var period = await db.OrderingPeriods.FirstOrDefaultAsync(p => p.Id == request.OrderingPeriodId, cancellationToken);
        if (period is null)
        {
            return Result.Failure<BatchOrderResult>(ErrorCodes.NotFound, "Az időszak nem található.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var settings = await db.AppSettings.FirstAsync(cancellationToken);

        // Small tables loaded in full rather than windowed by period range — simpler and safer than
        // guessing a lookback window, and matches GetOrderableDaysHandler's reasoning for this data.
        var excludedDates = await db.ExcludedDays.Select(e => e.Date).ToHashSetAsync(cancellationToken);
        var kitchenClosures = await db.KitchenClosures.Select(k => k.Date).ToHashSetAsync(cancellationToken);

        var dailyMenus = await db.DailyMenus
            .Include(m => m.Variants.Where(v => v.RemovedAtUtc == null))
            .Where(m => m.Date >= period.StartDate && m.Date <= period.EndDate && m.RemovedAtUtc == null)
            .ToDictionaryAsync(m => m.Date, cancellationToken);

        var userOrders = await db.MenuOrders
            .Where(o => o.UserId == request.TargetUserId && o.Status == OrderStatus.Active
                        && o.Date >= period.StartDate && o.Date <= period.EndDate)
            .Select(o => o.Date)
            .ToHashSetAsync(cancellationToken);

        var nowLocal = clock.LocalNow;
        var nowUtc = clock.UtcNow.UtcDateTime;

        // Two ordering phases (01-szerver-architektura.md 3.1 "A rendelés két fázisa"): A — bulk, no
        // throughput requirement, only open before OrderDeadline; B — supplementary, gated by CanChange
        // per day, only available while the period is still open and today hasn't passed its end date.
        var bulkWindow = period.IsOpen && nowLocal <= period.OrderDeadline;
        var supplementaryWindowOpen = period.IsOpen && nowLocal > period.OrderDeadline && clock.Today <= period.EndDate;

        var succeeded = new List<DayResult>();
        var skipped = new List<DaySkip>();

        foreach (var dayRequest in request.Days)
        {
            var date = dayRequest.Date;

            if (date < period.StartDate || date > period.EndDate)
            {
                skipped.Add(new DaySkip(date, ErrorCodes.OutsidePeriod));
                continue;
            }

            if (!workingDayCalculator.IsWorkingDay(date, ImmutableExcludedSet))
            {
                skipped.Add(new DaySkip(date, ErrorCodes.NotWorkingDay));
                continue;
            }

            if (excludedDates.Contains(date))
            {
                skipped.Add(new DaySkip(date, ErrorCodes.DayExcluded));
                continue;
            }

            if (kitchenClosures.Contains(date))
            {
                skipped.Add(new DaySkip(date, ErrorCodes.DayClosed));
                continue;
            }

            if (!dailyMenus.TryGetValue(date, out var menu) || !menu.IsPublished)
            {
                skipped.Add(new DaySkip(date, ErrorCodes.MenuNotPublished));
                continue;
            }

            var variant = menu.Variants.FirstOrDefault(v => v.Code == dayRequest.VariantCode);
            if (variant is null)
            {
                // Not one of the documented Skipped reasons (01-szerver-architektura.md 3.5) — a
                // well-behaved UI only ever submits a code it read from the same day's published menu,
                // so this is a defensive branch for a malformed/stale request, not an expected business
                // outcome. See the plan's "tervezési döntések" for why this isn't a thrown exception.
                skipped.Add(new DaySkip(date, ErrorCodes.InvalidVariantCode));
                continue;
            }

            if (userOrders.Contains(date))
            {
                skipped.Add(new DaySkip(date, ErrorCodes.AlreadyOrdered));
                continue;
            }

            if (!bulkWindow && !(supplementaryWindowOpen && workingDayCalculator.CanChange(date, nowLocal, settings, excludedDates, hasKitchenClosure: false)))
            {
                skipped.Add(new DaySkip(date, period.IsOpen ? ErrorCodes.DeadlinePassed : ErrorCodes.PeriodClosed));
                continue;
            }

            db.MenuOrders.Add(new MenuOrder
            {
                UserId = request.TargetUserId,
                Date = date,
                OrderingPeriodId = period.Id,
                MenuVariantId = variant.Id,
                PriceHuf = settings.MenuPortionHuf,
                Status = OrderStatus.Active,
                PlacedByUserId = request.PlacedByUserId,
                PlacedAtUtc = nowUtc,
            });

            userOrders.Add(date);
            succeeded.Add(new DayResult(date, variant.Code));
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A genuinely rare race: two concurrent requests for the same user+date both passed the
            // in-app userOrders pre-check above before either had saved, so the DB's filtered unique
            // index (MenuOrderConfiguration, (UserId, Date) WHERE Status = Active) is what actually
            // caught it. SaveChangesAsync is all-or-nothing per call, so nothing here was persisted —
            // there is no way to salvage a partial Succeeded/Skipped split, only to fail the whole
            // batch gracefully instead of letting a raw DbUpdateException escape (NFR-2).
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<BatchOrderResult>(
                ErrorCodes.AlreadyOrdered,
                "Időközben valaki más rendelést adott le ugyanerre a napra — próbáld újra.");
        }

        await transaction.CommitAsync(cancellationToken);

        return Result.Success(new BatchOrderResult(succeeded, skipped));
    }
}
