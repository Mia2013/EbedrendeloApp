using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Time;
using EbedrendeloApp.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.Notifications.MarkNotificationRead;

public sealed class MarkNotificationReadHandler(IDbContextFactory<EbedrendeloDbContext> dbFactory, IAppClock clock)
    : IRequestHandler<MarkNotificationReadCommand, Result>
{
    public async Task<Result> Handle(MarkNotificationReadCommand request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // A tulajdonos-szűrés a lekérdezésben van: más értesítését nem létezőként kezeljük, hogy az
        // azonosítóból ne derüljön ki, van-e ilyen.
        var notification = await db.UserNotifications
            .FirstOrDefaultAsync(n => n.Id == request.NotificationId && n.UserId == request.UserId, cancellationToken);

        if (notification is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "Az értesítés nem található.");
        }

        if (notification.ReadAtUtc is null)
        {
            notification.ReadAtUtc = clock.UtcNow.UtcDateTime;
            await db.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
