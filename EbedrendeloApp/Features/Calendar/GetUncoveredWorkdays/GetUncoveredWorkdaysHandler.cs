using EbedrendeloApp.Common.Calendar;
using EbedrendeloApp.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.Calendar.GetUncoveredWorkdays;

public sealed class GetUncoveredWorkdaysHandler(IDbContextFactory<EbedrendeloDbContext> dbFactory, IWorkingDayCalculator workingDayCalculator)
    : IRequestHandler<GetUncoveredWorkdaysQuery, IReadOnlyList<DateOnly>>
{
    public async Task<IReadOnlyList<DateOnly>> Handle(GetUncoveredWorkdaysQuery request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var periods = await db.OrderingPeriods
            .Where(p => p.StartDate <= request.To && p.EndDate >= request.From)
            .Select(p => new { p.StartDate, p.EndDate })
            .ToListAsync(cancellationToken);

        // Days that are explicitly excluded are reported separately (GetExcludedDaysQuery) — skip
        // them here so a day that is both excluded and outside any period doesn't show up twice.
        var excludedDates = await db.ExcludedDays
            .Where(e => e.Date >= request.From && e.Date <= request.To)
            .Select(e => e.Date)
            .ToHashSetAsync(cancellationToken);

        var result = new List<DateOnly>();
        for (var date = request.From; date <= request.To; date = date.AddDays(1))
        {
            if (!workingDayCalculator.IsWorkingDay(date, excludedDates))
            {
                continue;
            }

            var covered = periods.Any(p => p.StartDate <= date && p.EndDate >= date);
            if (!covered)
            {
                result.Add(date);
            }
        }

        return result;
    }
}
