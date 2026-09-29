using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Security;
using MediatR;

namespace EbedrendeloApp.Features.Notifications.MarkNotificationRead;

/// <summary>AC 8.1.2 — egy értesítés olvasottnak jelölése (a listában kattintásra). Már olvasott
/// értesítésre sikeres no-op: a kattintás nem hiba.</summary>
public sealed record MarkNotificationReadCommand(int UserId, int NotificationId) : IRequest<Result>, IActsOnBehalfOf
{
    int IActsOnBehalfOf.TargetUserId => UserId;
}
