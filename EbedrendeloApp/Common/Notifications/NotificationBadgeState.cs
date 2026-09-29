using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Features.Notifications.GetNotificationCounts;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace EbedrendeloApp.Common.Notifications;

/// <summary>
/// Az értesítések száma (összes + olvasatlan) egy körön (circuit) belül — ezt mutatja a menüpont, a
/// fejléc csengője és az értesítések oldalának szűrője. Egy példány, egy lekérdezés: a kijelzők nem
/// kérdeznek le külön-külön.
///
/// Frissül navigáláskor, felhasználóváltáskor, és minden sikeres parancs után
/// (<see cref="NotificationBadgeRefreshBehavior{TRequest, TResponse}"/>) — így a saját művelet (pl. egy
/// lemondás a naptárban) keltette értesítés azonnal látszik. Valós idejű push nincs: más felhasználó
/// művelete (pl. egy kizárás) a következő navigáláskor jelenik meg.
///
/// <c>IMediator</c>-on át kérdez, nem DbContexten — így megfelel a CLAUDE.md „komponens-szolgáltatás nem
/// injektál DbContextet" szabályának.
/// </summary>
public sealed class NotificationBadgeState(
    IMediator mediator,
    ICurrentUser currentUser,
    NavigationManager navigationManager,
    ILogger<NotificationBadgeState> logger) : IDisposable
{
    private readonly CancellationTokenSource disposal = new();
    private Task? initialLoad;
    private bool subscribed;
    private bool disposed;
    private int startedVersion;
    private int appliedVersion;

    public int Total { get; private set; }

    public int Unread { get; private set; }

    /// <summary>Csak akkor jelez, ha a számok ténylegesen változtak.</summary>
    public event Action? Changed;

    /// <summary>Az első betöltés — több kijelző is hívhatja, a lekérdezés egyszer fut. Hibás betöltés
    /// nem ragad be: a következő hívás újrapróbálja, a hiba pedig naplózva lesz, nem dönti le a
    /// menüt vagy a fejlécet (a számláló ilyenkor 0-n marad). A navigálás- és felhasználóváltás-
    /// figyelés is itt indul: amíg semmi nem mutatja a számlálót, nincs mit frissíteni.</summary>
    public async Task EnsureLoadedAsync()
    {
        Subscribe();

        // A közös Task-ot a várakozás KÖRÉ tett hibakezelés nullázza — ha a lekérdezés szinkron bukik,
        // egy a Task-on belüli nullázást a „??=" értékadás utólag felülírná, és a hiba beragadna.
        var load = initialLoad ??= RefreshAsync();
        try
        {
            await load;
        }
        catch (OperationCanceledException) when (disposal.IsCancellationRequested)
        {
            // A kör megszűnt betöltés közben — nincs kinek megmutatni a számot, ez nem hiba.
        }
        catch (Exception ex)
        {
            // Több kijelző is várhatja ugyanazt a hibás Task-ot — csak az első nulláz és naplóz, és csak
            // akkor, ha közben nem indult már újabb betöltés.
            if (ReferenceEquals(initialLoad, load))
            {
                initialLoad = null;
                logger.LogWarning(ex, "Az értesítések száma nem tölthető be.");
            }
        }
    }

    /// <summary>
    /// Átfedő frissítéseknél (navigálás, felhasználóváltás, parancs utáni frissítés) egy eredmény csak
    /// akkor kerül ki, ha nála később indult frissítés még nem került ki. Így egy lassabban visszatérő,
    /// korábban indult lekérdezés nem írhatja felül a frissebbet — de ha a később indult hibára fut, a
    /// korábbi sikeres eredmény nem vész el.
    /// </summary>
    public async Task RefreshAsync()
    {
        if (disposed)
        {
            return;
        }

        var version = ++startedVersion;
        var cancellationToken = disposal.Token;

        await currentUser.EnsureLoadedAsync(cancellationToken);
        if (!currentUser.IsLoaded)
        {
            return;
        }

        var counts = await mediator.Send(new GetNotificationCountsQuery(currentUser.UserId), cancellationToken);
        if (version <= appliedVersion)
        {
            return;
        }

        appliedVersion = version;
        Apply(counts.Total, counts.Unread);
    }

    /// <summary>Kényelmi frissítés: a hibát nem nyeljük el némán, de a számláló miatt nem is döntjük le a
    /// kört vagy az oldalt — naplózzuk, és a számláló a régi értéken marad.</summary>
    public async Task RefreshSafelyAsync()
    {
        try
        {
            await RefreshAsync();
        }
        catch (OperationCanceledException) when (disposal.IsCancellationRequested)
        {
            // A kör megszűnt frissítés közben — nincs kinek megmutatni a számot, ez nem hiba.
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Az értesítések száma nem frissíthető.");
        }
    }

    /// <summary>Frissítés, de csak ha valami már mutatja a számlálót (a kör egy kijelzője betöltötte) —
    /// a kijelző nélküli hívások (pl. tesztek, háttérfolyamat) így nem futtatnak fölösleges lekérdezést.</summary>
    public Task RefreshIfDisplayedAsync() => initialLoad is null ? Task.CompletedTask : RefreshSafelyAsync();

    private void Apply(int total, int unread)
    {
        if (total == Total && unread == Unread)
        {
            return;
        }

        Total = total;
        Unread = unread;
        Changed?.Invoke();
    }

    private void Subscribe()
    {
        if (subscribed)
        {
            return;
        }

        subscribed = true;
        navigationManager.LocationChanged += OnLocationChanged;
        currentUser.Changed += OnCurrentUserChanged;
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e) => _ = RefreshSafelyAsync();

    /// <summary>Az előző felhasználó száma egy pillanatig se látsszon az újnál: a még futó, régi
    /// felhasználóra indult lekérdezések eredménye érvénytelen, a számláló nulláról indul.</summary>
    private void OnCurrentUserChanged()
    {
        appliedVersion = startedVersion;
        Apply(0, 0);
        _ = RefreshSafelyAsync();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        navigationManager.LocationChanged -= OnLocationChanged;
        currentUser.Changed -= OnCurrentUserChanged;
        disposal.Cancel();
        disposal.Dispose();
    }
}
