namespace EbedrendeloApp.Common.Security;

// TODO(Epic 9 / US-9.1-9.3): replace with a cookie-authentication-backed implementation
// that reads claims from AuthenticationStateProvider. See 01-szerver-architektura.md 5. fejezet.
public interface ICurrentUser
{
    int UserId { get; }
    string UserName { get; }
    string DisplayName { get; }
    bool IsAdmin { get; }
    bool IsLoaded { get; }

    event Action? Changed;

    Task EnsureLoadedAsync(CancellationToken ct = default);
}
