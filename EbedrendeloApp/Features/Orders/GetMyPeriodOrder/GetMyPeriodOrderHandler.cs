using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.Orders.GetMyPeriodOrder;

public sealed class GetMyPeriodOrderHandler(IDbContextFactory<EbedrendeloDbContext> dbFactory)
    : IRequestHandler<GetMyPeriodOrderQuery, Result<IReadOnlyList<MyPeriodOrderDto>>>
{
    public async Task<Result<IReadOnlyList<MyPeriodOrderDto>>> Handle(GetMyPeriodOrderQuery request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var periodExists = await db.OrderingPeriods.AnyAsync(p => p.Id == request.OrderingPeriodId, cancellationToken);
        if (!periodExists)
        {
            return Result.Failure<IReadOnlyList<MyPeriodOrderDto>>(ErrorCodes.NotFound, "Az időszak nem található.");
        }

        var orders = await db.MenuOrders
            .Where(o => o.UserId == request.UserId && o.OrderingPeriodId == request.OrderingPeriodId)
            .OrderBy(o => o.Date)
            .ToListAsync(cancellationToken);

        var variantIds = orders.Select(o => o.MenuVariantId).Distinct().ToList();
        var variants = await db.MenuVariants
            .Where(v => variantIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, cancellationToken);

        // AC 3.3.3 — the placer's display name is only needed when someone other than the owner
        // placed the order on their behalf.
        var otherPlacerIds = orders.Where(o => o.PlacedByUserId != o.UserId)
            .Select(o => o.PlacedByUserId)
            .Distinct()
            .ToList();
        var placerNames = await db.Users
            .Where(u => otherPlacerIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => $"{u.VezetekNev} {u.KeresztNev}".Trim(), cancellationToken);

        var result = orders.Select(o =>
        {
            var variant = variants[o.MenuVariantId];
            var placedByDisplayName = o.PlacedByUserId != o.UserId ? placerNames.GetValueOrDefault(o.PlacedByUserId) : null;

            return new MyPeriodOrderDto(
                o.Date,
                o.Status,
                variant.Code,
                VariantDisplayName.Combine(variant.SoupName, variant.MainCourseName),
                o.PlacedByUserId,
                placedByDisplayName,
                o.PlacedAtUtc,
                o.CancellationReason,
                o.CancelledAtUtc,
                o.ReassignedFromVariantCode);
        }).ToList();

        return Result.Success<IReadOnlyList<MyPeriodOrderDto>>(result);
    }
}
