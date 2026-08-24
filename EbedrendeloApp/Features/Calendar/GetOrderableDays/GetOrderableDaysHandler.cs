using EbedrendeloApp.Common.Calendar;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Time;
using EbedrendeloApp.Data;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.Calendar.GetOrderableDays;

public sealed class GetOrderableDaysHandler(
    IDbContextFactory<EbedrendeloDbContext> dbFactory,
    IAppClock clock,
    IWorkingDayCalculator workingDayCalculator)
    : IRequestHandler<GetOrderableDaysQuery, Result<IReadOnlyList<OrderableDayDto>>>
{
    private static readonly IReadOnlySet<DateOnly> ImmutableExcludedSet = new HashSet<DateOnly>();

    public async Task<Result<IReadOnlyList<OrderableDayDto>>> Handle(GetOrderableDaysQuery request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var period = await db.OrderingPeriods.FirstOrDefaultAsync(p => p.Id == request.OrderingPeriodId, cancellationToken);
        if (period is null)
        {
            return Result.Failure<IReadOnlyList<OrderableDayDto>>(ErrorCodes.NotFound, "Az időszak nem található.");
        }

        var settings = await db.AppSettings.FirstAsync(cancellationToken);

        var excludedDaysInRange = await db.ExcludedDays
            .Where(e => e.Date <= period.EndDate && e.Date >= period.StartDate.AddDays(-60))
            .ToListAsync(cancellationToken);
        var excludedDates = excludedDaysInRange.Select(e => e.Date).ToHashSet();
        var excludedReasons = excludedDaysInRange.ToDictionary(e => e.Date, e => e.Reason);

        var kitchenClosures = await db.KitchenClosures
            .Where(k => k.Date >= period.StartDate && k.Date <= period.EndDate)
            .Select(k => k.Date)
            .ToHashSetAsync(cancellationToken);

        var dailyMenus = await db.DailyMenus
            .Include(m => m.Variants.Where(v => v.RemovedAtUtc == null))
            .Where(m => m.Date >= period.StartDate && m.Date <= period.EndDate && m.RemovedAtUtc == null)
            .ToDictionaryAsync(m => m.Date, cancellationToken);

        var userOrders = await db.MenuOrders
            .Where(o => o.UserId == request.UserId && o.Status == OrderStatus.Active
                        && o.Date >= period.StartDate && o.Date <= period.EndDate)
            .ToDictionaryAsync(o => o.Date, cancellationToken);

        var variantIds = userOrders.Values.Select(o => o.MenuVariantId).ToList();
        var variants = await db.MenuVariants
            .Where(v => variantIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, cancellationToken);

        var nowLocal = clock.LocalNow;
        var inOrderWindow = nowLocal <= period.OrderDeadline;

        var result = new List<OrderableDayDto>();

        for (var date = period.StartDate; date <= period.EndDate; date = date.AddDays(1))
        {
            // Weekend and "explicitly excluded" are checked separately (not via a single
            // IsWorkingDay(date, excludedDates) call) because this loop needs to tell them apart —
            // excluded days get an ErrorCodes.DayExcluded row, weekends get no row at all. Passing an
            // empty excluded-set here reuses the calculator's weekend rule without merging the two.
            if (!workingDayCalculator.IsWorkingDay(date, ImmutableExcludedSet))
            {
                continue;
            }

            if (excludedDates.Contains(date))
            {
                result.Add(new OrderableDayDto(date, false, false, null, null, ErrorCodes.DayExcluded, excludedReasons[date]));
                continue;
            }

            if (kitchenClosures.Contains(date))
            {
                result.Add(new OrderableDayDto(date, false, false, null, null, ErrorCodes.DayClosed, null));
                continue;
            }

            if (!dailyMenus.TryGetValue(date, out var menu) || !menu.IsPublished)
            {
                result.Add(new OrderableDayDto(date, false, false, null, null, ErrorCodes.MenuNotPublished, null));
                continue;
            }

            if (userOrders.TryGetValue(date, out var order))
            {
                var cancellable = workingDayCalculator.CanChange(date, nowLocal, settings, excludedDates, hasKitchenClosure: false);
                var variant = variants.GetValueOrDefault(order.MenuVariantId);
                var cancelReason = cancellable ? null : ErrorCodes.DeadlinePassed;
                result.Add(new OrderableDayDto(date, false, cancellable, variant?.Code, variant?.Name, cancellable ? ErrorCodes.AlreadyOrdered : cancelReason, null));
                continue;
            }

            var orderable = inOrderWindow || workingDayCalculator.CanChange(date, nowLocal, settings, excludedDates, hasKitchenClosure: false);
            var reason = orderable ? ErrorCodes.NoActiveOrder : ErrorCodes.DeadlinePassed;
            result.Add(new OrderableDayDto(date, orderable, false, null, null, reason, null));
        }

        return Result.Success<IReadOnlyList<OrderableDayDto>>(result);
    }
}
