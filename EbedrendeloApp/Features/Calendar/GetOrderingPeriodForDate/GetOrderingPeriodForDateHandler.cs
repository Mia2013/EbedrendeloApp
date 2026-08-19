using EbedrendeloApp.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.Calendar.GetOrderingPeriodForDate;

public sealed class GetOrderingPeriodForDateHandler(IDbContextFactory<EbedrendeloDbContext> dbFactory)
    : IRequestHandler<GetOrderingPeriodForDateQuery, OrderingPeriodDto?>
{
    public async Task<OrderingPeriodDto?> Handle(GetOrderingPeriodForDateQuery request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var period = await db.OrderingPeriods
            .FirstOrDefaultAsync(p => p.StartDate <= request.Date && p.EndDate >= request.Date, cancellationToken);

        if (period is null)
        {
            return null;
        }

        var hasOrders = await db.MenuOrders.AnyAsync(o => o.OrderingPeriodId == period.Id, cancellationToken);
        return new OrderingPeriodDto(period.Id, period.Name, period.StartDate, period.EndDate, period.OrderDeadline, period.IsOpen, hasOrders);
    }
}
