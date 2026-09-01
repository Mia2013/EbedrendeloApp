using EbedrendeloApp.Data;
using EbedrendeloApp.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.ALaCarte.GetALaCarteDailySummary;

public sealed class GetALaCarteDailySummaryHandler(IDbContextFactory<EbedrendeloDbContext> dbFactory)
    : IRequestHandler<GetALaCarteDailySummaryQuery, ALaCarteDailySummaryDto>
{
    public async Task<ALaCarteDailySummaryDto> Handle(GetALaCarteDailySummaryQuery request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var lines = await db.ALaCarteOrderLines
            .Where(l => l.ALaCarteOrder!.Date == request.Date)
            .ToListAsync(cancellationToken);

        var grouped = lines
            .GroupBy(l => (l.CategorySnapshot, l.ItemNameSnapshot))
            .Select(g => new ALaCarteSummaryLineDto(g.Key.CategorySnapshot, g.Key.ItemNameSnapshot, g.Count()))
            .OrderBy(l => l.Category).ThenBy(l => l.ItemName, StringComparer.Ordinal)
            .ToList();

        var soupPortionCount = lines.Count(l => l.CategorySnapshot == ALaCarteCategory.Foetel);

        return new ALaCarteDailySummaryDto(request.Date, soupPortionCount, grouped);
    }
}
