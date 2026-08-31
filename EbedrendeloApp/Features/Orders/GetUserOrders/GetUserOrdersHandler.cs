using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.Orders.GetUserOrders;

public sealed class GetUserOrdersHandler(IDbContextFactory<EbedrendeloDbContext> dbFactory)
    : IRequestHandler<GetUserOrdersQuery, Result<IReadOnlyList<UserOrderDto>>>
{
    public async Task<Result<IReadOnlyList<UserOrderDto>>> Handle(GetUserOrdersQuery request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var orders = await db.MenuOrders
            .Where(o => request.OrderingPeriodId == null || o.OrderingPeriodId == request.OrderingPeriodId)
            .Where(o => request.UserId == null || o.UserId == request.UserId)
            .Where(o => request.Status == null || o.Status == request.Status)
            .OrderBy(o => o.Date)
            .ToListAsync(cancellationToken);

        var variantIds = orders.Select(o => o.MenuVariantId).Distinct().ToList();
        var variants = await db.MenuVariants
            .Where(v => variantIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, cancellationToken);

        var userIds = orders.Select(o => o.UserId)
            .Concat(orders.Select(o => o.PlacedByUserId))
            .Concat(orders.Where(o => o.CancelledByUserId != null).Select(o => o.CancelledByUserId!.Value))
            .Distinct()
            .ToList();
        var userNames = await db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => $"{u.VezetekNev} {u.KeresztNev}".Trim(), cancellationToken);

        var result = orders.Select(o =>
        {
            var variant = variants[o.MenuVariantId];

            return new UserOrderDto(
                o.Id,
                o.Date,
                o.UserId,
                userNames.GetValueOrDefault(o.UserId, "Ismeretlen felhasználó"),
                variant.Code,
                VariantDisplayName.Combine(variant.SoupName, variant.MainCourseName),
                o.Status,
                o.PlacedByUserId,
                userNames.GetValueOrDefault(o.PlacedByUserId, "Ismeretlen felhasználó"),
                o.PlacedAtUtc,
                o.CancelledByUserId,
                o.CancelledByUserId is { } cancelledById ? userNames.GetValueOrDefault(cancelledById, "Ismeretlen felhasználó") : null,
                o.CancelledAtUtc,
                o.CancellationReason);
        }).ToList();

        return Result.Success<IReadOnlyList<UserOrderDto>>(result);
    }
}
