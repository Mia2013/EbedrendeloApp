using EbedrendeloApp.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.Calendar.GetOrderingPeriod;

public sealed class GetOrderingPeriodHandler(IDbContextFactory<EbedrendeloDbContext> dbFactory)
    : IRequestHandler<GetOrderingPeriodQuery, OrderingPeriodDto?>
{
    public async Task<OrderingPeriodDto?> Handle(GetOrderingPeriodQuery request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var period = await db.OrderingPeriods.FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
        if (period is null)
        {
            return null;
        }

        var hasOrders = await db.MenuOrders.AnyAsync(o => o.OrderingPeriodId == period.Id, cancellationToken);
        return new OrderingPeriodDto(period.Id, period.Name, period.StartDate, period.EndDate, period.OrderDeadline, period.IsOpen, hasOrders);
    }
}
