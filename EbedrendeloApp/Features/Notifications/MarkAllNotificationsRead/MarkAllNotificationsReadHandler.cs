using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Time;
using EbedrendeloApp.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.Notifications.MarkAllNotificationsRead;

public sealed class MarkAllNotificationsReadHandler(IDbContextFactory<EbedrendeloDbContext> dbFactory, IAppClock clock)
    : IRequestHandler<MarkAllNotificationsReadCommand, Result<int>>
{
    public async Task<Result<int>> Handle(MarkAllNotificationsReadCommand request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var nowUtc = clock.UtcNow.UtcDateTime;
        var marked = await db.UserNotifications
            .Where(n => n.UserId == request.UserId && n.ReadAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAtUtc, nowUtc), cancellationToken);

        return Result.Success(marked);
    }
}
