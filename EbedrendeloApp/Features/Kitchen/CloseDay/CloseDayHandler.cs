using System.Data;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Services;
using EbedrendeloApp.Common.Time;
using EbedrendeloApp.Data;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Orders;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.Kitchen.CloseDay;

public sealed class CloseDayHandler(IDbContextFactory<EbedrendeloDbContext> dbFactory, IAppClock clock)
    : IRequestHandler<CloseDayCommand, Result<KitchenClosureDto>>
{
    public async Task<Result<KitchenClosureDto>> Handle(CloseDayCommand request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Serializable, mirroring UpsertOrderingPeriodHandler's overlap check (01-szerver-architektura.md
        // 2./11. fejezet) — KitchenClosure has no DB-level uniqueness on "currently closed" any more
        // (a date can be closed more than once over its lifetime), so the double-close race is guarded
        // here at the app level instead.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        if (await KitchenClosureQueries.IsClosedAsync(db, request.Date, cancellationToken))
        {
            return Result.Failure<KitchenClosureDto>(ErrorCodes.DayClosed, "A nap már le van zárva.");
        }

        // AC 6.1.3 / 6.2.1: only Active orders count, and the variant Code/Name are snapshotted as they
        // are right now — a later menu edit must not retroactively change what this closure recorded.
        var grouped = await db.MenuOrders
            .Where(o => o.Date == request.Date && o.Status == OrderStatus.Active)
            .Join(db.MenuVariants, o => o.MenuVariantId, v => v.Id, (o, v) => v)
            .GroupBy(v => new { v.Code, v.SoupName, v.MainCourseName })
            .Select(g => new { g.Key.Code, g.Key.SoupName, g.Key.MainCourseName, Quantity = g.Count() })
            .ToListAsync(cancellationToken);

        var nowUtc = clock.UtcNow.UtcDateTime;

        var orderedLines = grouped
            .OrderBy(g => g.Code, StringComparer.Ordinal)
            .ToList();

        var closure = new KitchenClosure
        {
            Date = request.Date,
            ClosedAtUtc = nowUtc,
            ClosedByUserId = request.ClosedByUserId,
            TotalPortions = orderedLines.Sum(g => g.Quantity),
        };
        db.KitchenClosures.Add(closure);

        // KitchenClosureLine.KitchenClosureId is required — the closure needs a real Id first, so this
        // is a two-phase save (mirrors UpsertDailyMenuHandler's variant-then-reassignment ordering).
        await db.SaveChangesAsync(cancellationToken);

        var lines = orderedLines
            .Select(g => new KitchenClosureLine
            {
                KitchenClosureId = closure.Id,
                VariantCode = g.Code,
                VariantNameSnapshot = VariantDisplayName.Combine(g.SoupName, g.MainCourseName),
                Quantity = g.Quantity,
            })
            .ToList();
        db.KitchenClosureLines.AddRange(lines);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var closedByUser = await db.Users.FirstAsync(u => u.Id == request.ClosedByUserId, cancellationToken);
        var closedByUserName = $"{closedByUser.VezetekNev} {closedByUser.KeresztNev}".Trim();

        return Result.Success(new KitchenClosureDto(
            closure.Date,
            closure.ClosedAtUtc,
            closure.ClosedByUserId,
            closedByUserName,
            closure.TotalPortions,
            lines.Select(l => new KitchenClosureLineDto(l.VariantCode, l.VariantNameSnapshot, l.Quantity)).ToList(),
            ReopenedAtUtc: null,
            ReopenedByUserId: null,
            ReopenedByUserName: null));
    }
}
