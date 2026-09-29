using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Services;
using EbedrendeloApp.Common.Time;
using EbedrendeloApp.Data;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.Calendar.ExcludeDay;

public sealed class ExcludeDayHandler(
    IDbContextFactory<EbedrendeloDbContext> dbFactory,
    IAppClock clock,
    ICreditService creditService,
    INotificationService notificationService)
    : IRequestHandler<ExcludeDayCommand, Result>
{
    public async Task<Result> Handle(ExcludeDayCommand request, CancellationToken cancellationToken)
    {
        if (request.Date <= clock.Today)
        {
            return Result.Failure(ErrorCodes.NotFutureDate, "Csak jövőbeli nap zárható ki.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        if (await db.ExcludedDays.AnyAsync(e => e.Date == request.Date, cancellationToken))
        {
            return Result.Failure(ErrorCodes.DayExcluded, "Ez a nap már ki van zárva.");
        }

        if (await KitchenClosureQueries.IsClosedAsync(db, request.Date, cancellationToken))
        {
            return Result.Failure(ErrorCodes.DayClosed, "A nap már le van zárva.");
        }

        var nowUtc = clock.UtcNow.UtcDateTime;

        var excluded = new ExcludedDay
        {
            Date = request.Date,
            Reason = request.Reason,
            CreatedAtUtc = nowUtc,
            CreatedByUserId = request.CreatedByUserId,
        };
        db.ExcludedDays.Add(excluded);
        await db.SaveChangesAsync(cancellationToken);

        var affectedOrders = await db.MenuOrders
            .Where(o => o.Date == request.Date && o.Status == OrderStatus.Active)
            .ToListAsync(cancellationToken);

        foreach (var order in affectedOrders)
        {
            order.Status = OrderStatus.Cancelled;
            order.CancelledAtUtc = nowUtc;
            order.CancelledByUserId = request.CreatedByUserId;
            order.CancellationReason = CancellationReason.DayExcluded;
            order.CancelledByExcludedDayId = excluded.Id;

            var credit = creditService.IssueCancellationCredit(db, order, request.CreatedByUserId, nowUtc);
            notificationService.NotifyOrderCancelled(
                db,
                order,
                credit,
                new OrderNotificationText(
                    "Rendelésed lemondásra került",
                    credit is null
                        ? $"A(z) {request.Date:yyyy.MM.dd} nap kizárásra került ({request.Reason}), a rendelésed lemondásra került. Ez a nap még nem volt kiszámlázva, így nem kerül rá számlára."
                        : $"A(z) {request.Date:yyyy.MM.dd} nap kizárásra került ({request.Reason}), a rendelésed jóváírásra került."),
                new OrderNotificationText(
                    "Az általad leadott rendelés lemondásra került",
                    $"A(z) {request.Date:yyyy.MM.dd} nap kizárásra került ({request.Reason}), az általad leadott rendelés lemondásra került."),
                request.CreatedByUserId,
                nowUtc);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
