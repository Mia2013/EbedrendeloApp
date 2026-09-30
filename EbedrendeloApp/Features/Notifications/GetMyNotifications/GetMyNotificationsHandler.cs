using EbedrendeloApp.Common.Time;
using EbedrendeloApp.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.Notifications.GetMyNotifications;

public sealed class GetMyNotificationsHandler(IDbContextFactory<EbedrendeloDbContext> dbFactory, IAppClock clock)
    : IRequestHandler<GetMyNotificationsQuery, IReadOnlyList<NotificationDto>>
{
    public async Task<IReadOnlyList<NotificationDto>> Handle(GetMyNotificationsQuery request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var rows = await db.UserNotifications
            .Where(n => n.UserId == request.UserId && (!request.UnreadOnly || n.ReadAtUtc == null))
            .OrderByDescending(n => n.CreatedAtUtc).ThenByDescending(n => n.Id)
            .Take(GetMyNotificationsQuery.Limit)
            .Select(n => new
            {
                n.Id,
                n.Type,
                n.Title,
                n.Message,
                n.RelatedDate,
                n.CreatedAtUtc,
                IsRead = n.ReadAtUtc != null,
                // A leadó értesítésénél a rendelés tulajdonosa más, mint a címzett — az ő neve kell. Név
                // nélküli felhasználónál (pl. hiányos HR-import) a felhasználónév, hogy ne üres „ nevében"
                // jelenjen meg.
                OnBehalfOfName = db.MenuOrders
                    .Where(o => o.Id == n.RelatedMenuOrderId && o.UserId != n.UserId)
                    .Join(db.Users, o => o.UserId, u => u.Id, (_, u) =>
                        (u.VezetekNev + " " + u.KeresztNev).Trim() == ""
                            ? u.UserName
                            : (u.VezetekNev + " " + u.KeresztNev).Trim())
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(n => new NotificationDto(
                n.Id,
                n.Type,
                n.Title,
                n.Message,
                n.RelatedDate,
                clock.ToLocal(new DateTimeOffset(DateTime.SpecifyKind(n.CreatedAtUtc, DateTimeKind.Utc))),
                n.IsRead,
                n.OnBehalfOfName))
            .ToList();
    }
}
