using EbedrendeloApp.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.Calendar.GetUncoveredWorkdays;

public sealed class GetUncoveredWorkdaysHandler(IDbContextFactory<EbedrendeloDbContext> dbFactory)
    : IRequestHandler<GetUncoveredWorkdaysQuery, IReadOnlyList<DateOnly>>
{
    public async Task<IReadOnlyList<DateOnly>> Handle(GetUncoveredWorkdaysQuery request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var periods = await db.OrderingPeriods
            .Where(p => p.StartDate <= request.To && p.EndDate >= request.From)
            .Select(p => new { p.StartDate, p.EndDate })
            .ToListAsync(cancellationToken);

        var result = new List<DateOnly>();
        for (var date = request.From; date <= request.To; date = date.AddDays(1))
        {
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
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
