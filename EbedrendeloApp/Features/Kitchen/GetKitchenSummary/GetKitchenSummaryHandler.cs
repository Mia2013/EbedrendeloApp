using EbedrendeloApp.Common.Services;
using EbedrendeloApp.Data;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Orders;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.Kitchen.GetKitchenSummary;

public sealed class GetKitchenSummaryHandler(IDbContextFactory<EbedrendeloDbContext> dbFactory)
    : IRequestHandler<GetKitchenSummaryQuery, KitchenSummaryDto>
{
    public async Task<KitchenSummaryDto> Handle(GetKitchenSummaryQuery request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // AC 6.1.3: only Active orders count — cancelled ones don't show up in this join at all.
        var grouped = await db.MenuOrders
            .Where(o => o.Date == request.Date && o.Status == OrderStatus.Active)
            .Join(db.MenuVariants, o => o.MenuVariantId, v => v.Id, (o, v) => v)
            .GroupBy(v => new { v.Code, v.SoupName, v.MainCourseName })
            .Select(g => new { g.Key.Code, g.Key.SoupName, g.Key.MainCourseName, Quantity = g.Count() })
            .ToListAsync(cancellationToken);

        var lines = grouped
            .Select(g => new KitchenVariantLineDto(g.Code, VariantDisplayName.Combine(g.SoupName, g.MainCourseName), g.Quantity))
            .OrderBy(l => l.VariantCode, StringComparer.Ordinal)
            .ToList();

        var isClosed = await KitchenClosureQueries.IsClosedAsync(db, request.Date, cancellationToken);

        return new KitchenSummaryDto(request.Date, isClosed, lines, lines.Sum(l => l.Quantity));
    }
}
