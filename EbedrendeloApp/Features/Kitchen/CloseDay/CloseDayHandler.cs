using System.Data;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Services;
using EbedrendeloApp.Common.Time;
using EbedrendeloApp.Data;
using EbedrendeloApp.Domain.Entities;
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

        // AC 6.1.3 / 6.2.1: csak az Active rendelés számít, és a variáns kódja/neve a MOSTANI állapot
        // pillanatképe — egy későbbi menü-szerkesztés nem írhatja át visszamenőleg, mit rögzített ez a
        // zárás. A nem rendelt variáns is bekerül, 0 adaggal, hogy a pillanatkép ugyanazt mutassa,
        // amit a konyha záráskor a képernyőn látott.
        var live = await KitchenSummaryLines.LoadLiveVariantsAsync(db, request.Date, request.Date, cancellationToken);
        var ordered = await KitchenSummaryLines.LoadOrderedVariantsAsync(db, request.Date, request.Date, cancellationToken);
        var orderedLines = KitchenSummaryLines.Build(live, ordered);

        var nowUtc = clock.UtcNow.UtcDateTime;

        var closure = new KitchenClosure
        {
            Date = request.Date,
            ClosedAtUtc = nowUtc,
            ClosedByUserId = request.ClosedByUserId,
            TotalPortions = orderedLines.Sum(l => l.Quantity),
        };
        db.KitchenClosures.Add(closure);

        // KitchenClosureLine.KitchenClosureId is required — the closure needs a real Id first, so this
        // is a two-phase save (mirrors UpsertDailyMenuHandler's variant-then-reassignment ordering).
        await db.SaveChangesAsync(cancellationToken);

        var lines = orderedLines
            .Select(l => new KitchenClosureLine
            {
                KitchenClosureId = closure.Id,
                VariantCode = l.VariantCode,
                VariantNameSnapshot = l.VariantName,
                Quantity = l.Quantity,
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
