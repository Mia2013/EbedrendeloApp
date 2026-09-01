using EbedrendeloApp.Data;
using EbedrendeloApp.Features.ALaCarte.GetALaCarteDailySummary;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.ALaCarte.GetALaCarteMonthlySummary;

public sealed class GetALaCarteMonthlySummaryHandler(IDbContextFactory<EbedrendeloDbContext> dbFactory)
    : IRequestHandler<GetALaCarteMonthlySummaryQuery, ALaCarteMonthlySummaryDto>
{
    public async Task<ALaCarteMonthlySummaryDto> Handle(GetALaCarteMonthlySummaryQuery request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var startDate = new DateOnly(request.Year, request.Month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        var lines = await db.ALaCarteOrderLines
            .Where(l => l.ALaCarteOrder!.Date >= startDate && l.ALaCarteOrder!.Date <= endDate)
            .ToListAsync(cancellationToken);

        var grouped = lines
            .GroupBy(l => (l.CategorySnapshot, l.ItemNameSnapshot))
            .Select(g => new ALaCarteSummaryLineDto(g.Key.CategorySnapshot, g.Key.ItemNameSnapshot, g.Count()))
            .OrderBy(l => l.Category).ThenBy(l => l.ItemName, StringComparer.Ordinal)
            .ToList();

        return new ALaCarteMonthlySummaryDto(request.Year, request.Month, grouped);
    }
}
