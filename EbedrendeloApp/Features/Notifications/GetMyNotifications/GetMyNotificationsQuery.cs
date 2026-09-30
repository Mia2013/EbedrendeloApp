using EbedrendeloApp.Common.Security;
using MediatR;

namespace EbedrendeloApp.Features.Notifications.GetMyNotifications;

/// <summary>AC 8.1.2 — a felhasználó legutóbbi értesítései, a legújabb elöl. Szándékosan fix
/// <see cref="Limit"/> darab, lapozás nélkül: az értesítés friss eseményről szól, a régebbiekre nincs
/// szükség. Az olvasatlanok száma ettől függetlenül a <c>GetNotificationCountsQuery</c>-ből jön, és az
/// „összes olvasottnak jelölése" a listán túliakat is jelöli.</summary>
public sealed record GetMyNotificationsQuery(int UserId, bool UnreadOnly = false)
    : IRequest<IReadOnlyList<NotificationDto>>, IActsOnBehalfOf
{
    public const int Limit = 20;

    int IActsOnBehalfOf.TargetUserId => UserId;
}
