using EbedrendeloApp.Common.Security;

namespace EbedrendeloApp.Tests.TestSupport;

public sealed class FakeCurrentUser : ICurrentUser, IDevUserSwitcher
{
    private readonly bool isAdmin;

    public FakeCurrentUser(int userId, string displayName, bool isAdmin)
    {
        UserId = userId;
        UserName = displayName;
        DisplayName = displayName;
        this.isAdmin = isAdmin;
    }

    public int UserId { get; private set; }

    public string UserName { get; private set; }

    public string DisplayName { get; private set; }

    public bool IsAdmin => isAdmin;

    public bool IsLoaded => true;

    public event Action? Changed;

    public Task EnsureLoadedAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task<IReadOnlyList<DevUserOption>> GetUsersAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<DevUserOption>>([new DevUserOption(UserId, DisplayName, isAdmin ? "Admin" : "User")]);

    public Task SwitchToAsync(int newUserId, CancellationToken ct = default)
    {
        UserId = newUserId;
        Changed?.Invoke();
        return Task.CompletedTask;
    }
}
