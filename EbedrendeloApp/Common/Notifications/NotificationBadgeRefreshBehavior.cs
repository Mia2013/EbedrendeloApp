using EbedrendeloApp.Common.Results;
using MediatR;

namespace EbedrendeloApp.Common.Notifications;

/// <summary>
/// Minden sikeres parancs után frissíti a kör értesítés-számlálóját (<see cref="NotificationBadgeState"/>).
/// Egy parancs a végrehajtójának is keletkeztethet értesítést (pl. a saját lemondás jóváírásáról), és ezt
/// a csengőnek azonnal mutatnia kell — nem csak a következő navigáláskor. Egy helyen, a pipeline-ban, hogy
/// ne kelljen minden oldalnak külön emlékeznie rá.
///
/// Parancs az, aminek a neve <c>Command</c>-ra végződik — a CLAUDE.md use case-mintája ezt kötelezővé
/// teszi, és a <c>NotificationBadgeRefreshBehaviorTests</c> őrzi. Lekérdezés után nincs frissítés (az
/// értesítést nem változtat), ahogy sikertelen parancs után sem.
/// </summary>
public sealed class NotificationBadgeRefreshBehavior<TRequest, TResponse>(NotificationBadgeState badge)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private static readonly bool IsCommand = typeof(TRequest).Name.EndsWith("Command", StringComparison.Ordinal);

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var response = await next(cancellationToken);

        if (IsCommand && response is Result { IsSuccess: true })
        {
            await badge.RefreshIfDisplayedAsync();
        }

        return response;
    }
}
