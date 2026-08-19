using EbedrendeloApp.Data;
using EbedrendeloApp.Data.Seed;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Common.Security;

public sealed class StubCurrentUser(IDbContextFactory<EbedrendeloDbContext> dbFactory) : ICurrentUser, IDevUserSwitcher
{
    private const string DefaultUserName = "admin";

    private readonly SemaphoreSlim _lock = new(1, 1);
    private CurrentUserInfo _current = CurrentUserInfo.Unloaded;

    public int UserId => _current.UserId;
    public string UserName => _current.UserName;
    public string DisplayName => _current.DisplayName;
    public bool IsAdmin => _current.IsAdmin;
    public bool IsLoaded => _current.IsLoaded;

    public event Action? Changed;

    public async Task EnsureLoadedAsync(CancellationToken ct = default)
    {
        if (_current.IsLoaded)
        {
            return;
        }

        await _lock.WaitAsync(ct);
        try
        {
            if (_current.IsLoaded)
            {
                return;
            }

            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var user = await db.Users.Include(u => u.Role)
                .FirstAsync(u => u.UserName == DefaultUserName, ct);
            _current = CurrentUserInfo.From(user);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<DevUserOption>> GetUsersAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Users.Include(u => u.Role)
            .OrderBy(u => u.VezetekNev).ThenBy(u => u.KeresztNev)
            .Select(u => new DevUserOption(u.Id, (u.VezetekNev + " " + u.KeresztNev).Trim(), u.Role!.Name))
            .ToListAsync(ct);
    }

    public async Task SwitchToAsync(int userId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var user = await db.Users.Include(u => u.Role).FirstAsync(u => u.Id == userId, ct);
        _current = CurrentUserInfo.From(user);
        Changed?.Invoke();
    }

    private readonly record struct CurrentUserInfo(int UserId, string UserName, string DisplayName, bool IsAdmin, bool IsLoaded)
    {
        public static readonly CurrentUserInfo Unloaded = new(0, string.Empty, string.Empty, false, false);

        public static CurrentUserInfo From(Domain.Entities.User user) => new(
            user.Id,
            user.UserName,
            $"{user.VezetekNev} {user.KeresztNev}".Trim(),
            user.Role?.Name == DatabaseSeeder.AdminRoleName,
            true);
    }
}
