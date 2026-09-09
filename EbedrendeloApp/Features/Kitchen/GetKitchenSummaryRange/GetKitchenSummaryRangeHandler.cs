using EbedrendeloApp.Common.Services;
using EbedrendeloApp.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.Kitchen.GetKitchenSummaryRange;

public sealed class GetKitchenSummaryRangeHandler(IDbContextFactory<EbedrendeloDbContext> dbFactory)
    : IRequestHandler<GetKitchenSummaryRangeQuery, IReadOnlyList<KitchenSummaryDto>>
{
    public async Task<IReadOnlyList<KitchenSummaryDto>> Handle(GetKitchenSummaryRangeQuery request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var live = await KitchenSummaryLines.LoadLiveVariantsAsync(db, request.From, request.To, cancellationToken);
        var ordered = await KitchenSummaryLines.LoadOrderedVariantsAsync(db, request.From, request.To, cancellationToken);

        var closedDates = await KitchenClosureQueries.GetClosedDatesAsync(db, request.From, request.To, cancellationToken);

        var liveByDate = live.ToLookup(v => v.Date);
        var orderedByDate = ordered.ToLookup(o => o.Date);

        // A publikált menüvel rendelkező nap akkor is szerepel, ha egyetlen rendelés sincs rá — így
        // látszik, hogy tényleg nincs mit főzni, nem pedig az, hogy a nap kimaradt a lekérdezésből.
        var dates = liveByDate.Select(g => g.Key)
            .Union(orderedByDate.Select(g => g.Key))
            .OrderBy(d => d);

        return dates
            .Select(date =>
            {
                var lines = KitchenSummaryLines.Build(liveByDate[date], orderedByDate[date]);
                return new KitchenSummaryDto(date, closedDates.Contains(date), lines, lines.Sum(l => l.Quantity));
            })
            .ToList();
    }
}
