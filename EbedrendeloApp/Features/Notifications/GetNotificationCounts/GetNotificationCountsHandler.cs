using EbedrendeloApp.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.Notifications.GetNotificationCounts;

public sealed class GetNotificationCountsHandler(IDbContextFactory<EbedrendeloDbContext> dbFactory)
    : IRequestHandler<GetNotificationCountsQuery, NotificationCountsDto>
{
    public async Task<NotificationCountsDto> Handle(GetNotificationCountsQuery request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Minden navigáláskor fut (a csengő számlálója), ezért egy lekérdezés, nem két COUNT.
        var counts = await db.UserNotifications
            .Where(n => n.UserId == request.UserId)
            .GroupBy(_ => 1)
            .Select(g => new NotificationCountsDto(g.Count(), g.Count(n => n.ReadAtUtc == null)))
            .FirstOrDefaultAsync(cancellationToken);

        return counts ?? new NotificationCountsDto(0, 0);
    }
}
