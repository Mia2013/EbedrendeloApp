using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Services;
using EbedrendeloApp.Common.Time;
using EbedrendeloApp.Data;
using EbedrendeloApp.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.Menus.DeleteDailyMenu;

public sealed class DeleteDailyMenuHandler(
    IDbContextFactory<EbedrendeloDbContext> dbFactory,
    IAppClock clock,
    ICreditService creditService,
    INotificationService notificationService)
    : IRequestHandler<DeleteDailyMenuCommand, Result>
{
    public async Task<Result> Handle(DeleteDailyMenuCommand request, CancellationToken cancellationToken)
    {
        if (request.Date < clock.Today)
        {
            return Result.Failure(ErrorCodes.NotFutureDate, "Elmúlt napi menü már nem törölhető.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        if (await KitchenClosureQueries.IsClosedAsync(db, request.Date, cancellationToken))
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

        var nowUtc = clock.UtcNow.UtcDateTime;

        var affectedOrders = await db.MenuOrders
            .Where(o => o.Date == request.Date && o.Status == OrderStatus.Active)
            .ToListAsync(cancellationToken);

        foreach (var order in affectedOrders)
        {
            order.Status = OrderStatus.Cancelled;
            order.CancelledAtUtc = nowUtc;
            order.CancelledByUserId = request.PerformedByUserId;
            order.CancellationReason = CancellationReason.MenuDeleted;

            var credit = creditService.IssueCancellationCredit(db, order, request.PerformedByUserId, nowUtc);
            notificationService.NotifyOrderCancelled(
                db,
                order,
                credit,
                new OrderNotificationText(
                    "Rendelésed lemondásra került",
                    credit is null
                        ? $"A(z) {request.Date:yyyy.MM.dd} napi menü törlésre került, a rendelésed lemondásra került. Ez a nap még nem volt kiszámlázva, így nem kerül rá számlára."
                        : $"A(z) {request.Date:yyyy.MM.dd} napi menü törlésre került, a rendelésed jóváírásra került."),
                new OrderNotificationText(
                    "Az általad leadott rendelés lemondásra került",
                    $"A(z) {request.Date:yyyy.MM.dd} napi menü törlésre került, az általad leadott rendelés lemondásra került."),
                request.PerformedByUserId,
                nowUtc);
        }

        menu.IsPublished = false;
        menu.RemovedAtUtc = nowUtc;
        foreach (var variant in menu.Variants)
        {
            variant.RemovedAtUtc = nowUtc;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
