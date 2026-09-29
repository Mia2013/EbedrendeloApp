using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Features.Notifications.GetNotificationCounts;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace EbedrendeloApp.Common.Notifications;

/// <summary>
/// Az olvasatlan értesítések száma egy körön (circuit) belül — ezt mutatja a menüpont és a fejléc
/// csengője. Egy példány, egy lekérdezés: a két kijelző nem kérdez le külön-külön.
///
/// Frissül navigáláskor és felhasználóváltáskor, valamint amikor az értesítések oldala jelölés után
/// <see cref="RefreshSafelyAsync"/>-et hív. Valós idejű push nincs: más felhasználó művelete (pl. egy
/// kizárás) a következő navigáláskor jelenik meg.
///
/// <c>IMediator</c>-on át kérdez, nem DbContexten — így megfelel a CLAUDE.md „komponens-szolgáltatás nem
/// injektál DbContextet" szabályának.
/// </summary>
public sealed class NotificationBadgeState : IDisposable
{
    private readonly IMediator mediator;
    private readonly ICurrentUser currentUser;
    private readonly NavigationManager navigationManager;
    private readonly ILogger<NotificationBadgeState> logger;
    private Task? initialLoad;
    private int refreshVersion;

    public NotificationBadgeState(
        IMediator mediator,
        ICurrentUser currentUser,
        NavigationManager navigationManager,
        ILogger<NotificationBadgeState> logger)
    {
        this.mediator = mediator;
        this.currentUser = currentUser;
        this.navigationManager = navigationManager;
        this.logger = logger;

        navigationManager.LocationChanged += OnLocationChanged;
        currentUser.Changed += OnCurrentUserChanged;
    }

    public int Unread { get; private set; }

    public event Action? Changed;

    /// <summary>Az első betöltés — több kijelző is hívhatja, a lekérdezés egyszer fut. Hibás betöltés
    /// nem ragad be: a következő hívás újrapróbálja, a hiba pedig naplózva lesz, nem dönti le a
    /// menüt vagy a fejlécet (a számláló ilyenkor 0-n marad).</summary>
    public async Task EnsureLoadedAsync()
    {
        // A közös Task-ot a várakozás KÖRÉ tett hibakezelés nullázza — ha a lekérdezés szinkron bukik,
        // egy a Task-on belüli nullázást a „??=" értékadás utólag felülírná, és a hiba beragadna.
        var load = initialLoad ??= RefreshAsync();
        try
        {
            await load;
        }
        catch (Exception ex)
        {
            // Több kijelző is várhatja ugyanazt a hibás Task-ot — csak az első nulláz és naplóz, és csak
            // akkor, ha közben nem indult már újabb betöltés.
            if (ReferenceEquals(initialLoad, load))
            {
                initialLoad = null;
                logger.LogWarning(ex, "Az olvasatlan értesítések száma nem tölthető be.");
            }
        }
    }

    /// <summary>Átfedő frissítéseknél (navigálás, felhasználóváltás, jelölés utáni frissítés) mindig a
    /// legutoljára indított eredménye marad meg — egy lassabban visszatérő, korábban indult lekérdezés
    /// nem írhatja felül egy frissebb értékkel.</summary>
    public async Task RefreshAsync()
    {
        var version = ++refreshVersion;

        await currentUser.EnsureLoadedAsync();
        if (!currentUser.IsLoaded)
        {
            return;
        }

        var counts = await mediator.Send(new GetNotificationCountsQuery(currentUser.UserId));
        if (version != refreshVersion)
        {
            return;
        }

        Unread = counts.Unread;
        Changed?.Invoke();
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e) => _ = RefreshSafelyAsync();

    private void OnCurrentUserChanged() => _ = RefreshSafelyAsync();

    /// <summary>Kényelmi frissítés (eseménykezelőből vagy egy sikeres jelölés után): a hibát nem nyeljük
    /// el némán, de a számláló miatt nem is döntjük le a kört vagy az oldalt — naplózzuk, és a számláló a
    /// régi értéken marad.</summary>
    public async Task RefreshSafelyAsync()
    {
        try
        {
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Az olvasatlan értesítések száma nem frissíthető.");
        }
    }

    public void Dispose()
    {
        navigationManager.LocationChanged -= OnLocationChanged;
        currentUser.Changed -= OnCurrentUserChanged;
    }
}
