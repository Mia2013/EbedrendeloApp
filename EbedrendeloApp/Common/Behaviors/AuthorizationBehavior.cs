using EbedrendeloApp.Common.Security;
using MediatR;

namespace EbedrendeloApp.Common.Behaviors;

/// <summary>
/// Szerveroldali jogosultság-ellenőrzés minden use case előtt, a <see cref="IRequireAdmin"/> és
/// <see cref="IActsOnBehalfOf"/> jelölők alapján. A <c>ValidationBehavior</c> ELŐTT fut: előbb dől el,
/// hogy a hívó egyáltalán kérheti-e ezt, és csak utána, hogy jól kérte-e.
/// </summary>
public sealed class AuthorizationBehavior<TRequest, TResponse>(
    ICurrentUser currentUser,
    ILogger<AuthorizationBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not (IRequireAdmin or IActsOnBehalfOf))
        {
            return await next(cancellationToken);
        }

        // A háttérhívások (pl. dialógusból induló parancs) nem feltétlenül várták meg a felhasználó
        // betöltését — itt kell, mert erre alapozunk.
        await currentUser.EnsureLoadedAsync(cancellationToken);

        if (request is IRequireAdmin && !currentUser.IsAdmin)
        {
            logger.LogWarning(
                "Elutasított admin művelet: {RequestType}, hívó: {UserId} ({UserName})",
                typeof(TRequest).Name, currentUser.UserId, currentUser.UserName);
            throw new ForbiddenException("Ehhez a művelethez adminisztrátori jogosultság szükséges.");
        }

        if (request is IActsOnBehalfOf onBehalfOf
            && onBehalfOf.TargetUserId != currentUser.UserId
            && !currentUser.IsAdmin)
        {
            logger.LogWarning(
                "Elutasított idegen nevében végzett művelet: {RequestType}, hívó: {UserId}, cél: {TargetUserId}",
                typeof(TRequest).Name, currentUser.UserId, onBehalfOf.TargetUserId);
            throw new ForbiddenException("Csak a saját nevedben végezheted ezt a műveletet.");
        }

        return await next(cancellationToken);
    }
}
