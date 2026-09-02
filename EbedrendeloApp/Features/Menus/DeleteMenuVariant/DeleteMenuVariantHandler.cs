using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Services;
using EbedrendeloApp.Common.Time;
using EbedrendeloApp.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.Menus.DeleteMenuVariant;

public sealed class DeleteMenuVariantHandler(
    IDbContextFactory<EbedrendeloDbContext> dbFactory,
    IAppClock clock,
    IMenuReassignmentService reassignmentService)
    : IRequestHandler<DeleteMenuVariantCommand, Result>
{
    public async Task<Result> Handle(DeleteMenuVariantCommand request, CancellationToken cancellationToken)
    {
        if (request.Date < clock.Today)
        {
            return Result.Failure(ErrorCodes.NotFutureDate, "Elmúlt nap variánsa már nem törölhető.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        if (await db.KitchenClosures.AnyAsync(k => k.Date == request.Date, cancellationToken))
        {
            return Result.Failure(ErrorCodes.DayClosed, "A nap már le van zárva.");
        }

        var menu = await db.DailyMenus
            .Include(m => m.Variants.Where(v => v.RemovedAtUtc == null))
            .FirstOrDefaultAsync(m => m.Date == request.Date && m.RemovedAtUtc == null, cancellationToken);
        if (menu is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "Erre a napra nincs menü.");
        }

        var variant = menu.Variants.FirstOrDefault(v => v.Code == request.VariantCode);
        if (variant is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "Ez a variáns nem található.");
        }

        var remaining = menu.Variants.Where(v => v.Id != variant.Id).ToList();
        var nowUtc = clock.UtcNow.UtcDateTime;

        await reassignmentService.ReassignOrCancelAsync(db, request.Date, variant, remaining, request.PerformedByUserId, nowUtc, cancellationToken);

        variant.RemovedAtUtc = nowUtc;

        if (remaining.Count == 0)
        {
            // No variants left on this day — treat it the same as an unpublished/removed menu (mirrors
            // DeleteDailyMenuHandler's IsPublished = false) so GetOrderableDaysHandler, GetTodayMenuForUserHandler
            // and GetPeriodMenuHandler stop reporting the day as orderable with nothing to actually order.
            // Without this, deleting the last variant silently strands a "published" day with an empty
            // Variants list (the UpsertDailyMenuValidator's "at least one variant" rule only guards the
            // save/upsert path, not this direct delete).
            menu.IsPublished = false;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
