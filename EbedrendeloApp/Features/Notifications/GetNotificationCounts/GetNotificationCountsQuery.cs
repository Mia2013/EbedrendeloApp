using EbedrendeloApp.Common.Security;
using MediatR;

namespace EbedrendeloApp.Features.Notifications.GetNotificationCounts;

/// <summary>Az összes és az olvasatlan értesítések száma — a menüpont és a fejléc csengőjének
/// számlálója, valamint az oldal szűrő-chipjei. Külön query, mert a számlálót minden navigáláskor
/// lekérjük, és ahhoz nem kell a lista.</summary>
public sealed record GetNotificationCountsQuery(int UserId) : IRequest<NotificationCountsDto>, IActsOnBehalfOf
{
    int IActsOnBehalfOf.TargetUserId => UserId;
}
