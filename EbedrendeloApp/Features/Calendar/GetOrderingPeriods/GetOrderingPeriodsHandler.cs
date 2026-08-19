using EbedrendeloApp.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.Calendar.GetOrderingPeriods;

public sealed class GetOrderingPeriodsHandler(IDbContextFactory<EbedrendeloDbContext> dbFactory)
    : IRequestHandler<GetOrderingPeriodsQuery, IReadOnlyList<OrderingPeriodDto>>
{
    public async Task<IReadOnlyList<OrderingPeriodDto>> Handle(GetOrderingPeriodsQuery request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var periodIdsWithOrders = await db.MenuOrders.Select(o => o.OrderingPeriodId).Distinct().ToHashSetAsync(cancellationToken);

        return await db.OrderingPeriods
            .OrderBy(p => p.StartDate)
            .Select(p => new OrderingPeriodDto(p.Id, p.Name, p.StartDate, p.EndDate, p.OrderDeadline, p.IsOpen, periodIdsWithOrders.Contains(p.Id)))
            .ToListAsync(cancellationToken);
    }
}
