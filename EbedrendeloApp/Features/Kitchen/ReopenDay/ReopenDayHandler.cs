using System.Data;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Time;
using EbedrendeloApp.Data;
using EbedrendeloApp.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.Kitchen.ReopenDay;

public sealed class ReopenDayHandler(IDbContextFactory<EbedrendeloDbContext> dbFactory, IAppClock clock)
    : IRequestHandler<ReopenDayCommand, Result>
{
    public async Task<Result> Handle(ReopenDayCommand request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var closure = await db.KitchenClosures
            .Where(k => k.Date == request.Date && k.Reopening == null)
            .OrderByDescending(k => k.ClosedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (closure is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "A napra nincs érvényben lévő zárás.");
        }

        // Only ever inserts — the closure row and its Lines are never touched, so the original snapshot
        // survives the reopen unchanged (AC 6.3.2).
        db.KitchenClosureReopenings.Add(new KitchenClosureReopening
        {
            KitchenClosureId = closure.Id,
            ReopenedAtUtc = clock.UtcNow.UtcDateTime,
            ReopenedByUserId = request.ReopenedByUserId,
        });

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
