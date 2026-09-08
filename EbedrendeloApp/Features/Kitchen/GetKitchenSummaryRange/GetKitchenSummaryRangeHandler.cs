using EbedrendeloApp.Common.Services;
using EbedrendeloApp.Data;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Orders;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.Kitchen.GetKitchenSummaryRange;

public sealed class GetKitchenSummaryRangeHandler(IDbContextFactory<EbedrendeloDbContext> dbFactory)
    : IRequestHandler<GetKitchenSummaryRangeQuery, IReadOnlyList<KitchenSummaryDto>>
{
    public async Task<IReadOnlyList<KitchenSummaryDto>> Handle(GetKitchenSummaryRangeQuery request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var grouped = await db.MenuOrders
            .Where(o => o.Date >= request.From && o.Date <= request.To && o.Status == OrderStatus.Active)
            .Join(db.MenuVariants, o => o.MenuVariantId, v => v.Id, (o, v) => new { o.Date, v.Code, v.SoupName, v.MainCourseName })
            .GroupBy(x => new { x.Date, x.Code, x.SoupName, x.MainCourseName })
            .Select(g => new { g.Key.Date, g.Key.Code, g.Key.SoupName, g.Key.MainCourseName, Quantity = g.Count() })
            .ToListAsync(cancellationToken);

        var closedDates = await KitchenClosureQueries.GetClosedDatesAsync(db, request.From, request.To, cancellationToken);

        var result = grouped
            .GroupBy(x => x.Date)
            .Select(dayGroup =>
            {
                var lines = dayGroup
                    .Select(g => new KitchenVariantLineDto(g.Code, VariantDisplayName.Combine(g.SoupName, g.MainCourseName), g.Quantity))
                    .OrderBy(l => l.VariantCode, StringComparer.Ordinal)
                    .ToList();
                return new KitchenSummaryDto(dayGroup.Key, closedDates.Contains(dayGroup.Key), lines, lines.Sum(l => l.Quantity));
            })
            .OrderBy(d => d.Date)
            .ToList();

        return result;
    }
}
