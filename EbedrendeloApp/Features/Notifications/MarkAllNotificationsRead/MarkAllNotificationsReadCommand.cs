using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Security;
using MediatR;

namespace EbedrendeloApp.Features.Notifications.MarkAllNotificationsRead;

/// <summary>AC 8.1.2 — minden olvasatlan értesítés olvasottnak jelölése, a listán (legutóbbi 20)
/// túliaké is. A visszatérési érték a most jelölt értesítések száma.</summary>
public sealed record MarkAllNotificationsReadCommand(int UserId) : IRequest<Result<int>>, IActsOnBehalfOf
{
    int IActsOnBehalfOf.TargetUserId => UserId;
}
