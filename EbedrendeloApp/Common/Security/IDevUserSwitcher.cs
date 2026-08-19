namespace EbedrendeloApp.Common.Security;

/// <summary>
/// Dev-only convenience for switching the in-memory <see cref="ICurrentUser"/> during a Blazor
/// circuit, so both admin and worker views can be tried without real authentication. Not part of
/// Epic 9 — the real dev-login (US-9.1) will replace this together with <see cref="ICurrentUser"/>.
/// </summary>
public interface IDevUserSwitcher
{
    Task<IReadOnlyList<DevUserOption>> GetUsersAsync(CancellationToken ct = default);
    Task SwitchToAsync(int userId, CancellationToken ct = default);
}

public sealed record DevUserOption(int UserId, string DisplayName, string RoleName);
