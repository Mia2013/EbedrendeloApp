using System.Reflection;
using EbedrendeloApp.Common.Security;
using MediatR;

namespace EbedrendeloApp.Tests.Common.Behaviors;

/// <summary>
/// Az <c>AuthorizationBehavior</c> csak azt védi, ami meg van jelölve — egy jelöletlenül becsúszó új
/// use case csendben védtelen maradna. Ez a teszt ezért minden MediatR kérésről számot kér: vagy visel
/// jelölőt, vagy szerepel az alábbi, szándékosan nyitott listán. Új use case felvételekor a teszt
/// elbukik, amíg valaki el nem dönti, melyik csoportba tartozik.
/// </summary>
public class UseCaseAuthorizationCoverageTests
{
    /// <summary>Szándékosan bárki által hívható: nincs bennük felhasználóra szűrt vagy admin-adat.</summary>
    private static readonly HashSet<string> IntentionallyUnrestricted =
    [
        // Törzsadat, amit a dolgozói naptár és a mai menü is olvas.
        "GetOrderingPeriodsQuery",
        "GetOrderingPeriodQuery",
        "GetOrderingPeriodForDateQuery",
        "GetPeriodMenuQuery",
        // A kolléga azonosítása névvel + igazgatósággal + osztállyal — épp az a lépés, ami a más
        // nevében rendelést feltételhez köti, ezért a dolgozónak is elérhetőnek kell lennie.
        "ResolveColleagueQuery",
    ];

    private static IEnumerable<Type> AllRequestTypes()
        => typeof(EbedrendeloApp.Program).Assembly
            .GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false }
                        && t.GetInterfaces().Any(i => i.IsGenericType
                                                      && (i.GetGenericTypeDefinition() == typeof(IRequest<>)
                                                          || i.GetGenericTypeDefinition() == typeof(IRequest))));

    [Fact]
    public void Every_use_case_is_either_marked_or_explicitly_unrestricted()
    {
        var unaccounted = AllRequestTypes()
            .Where(t => !typeof(IRequireAdmin).IsAssignableFrom(t)
                        && !typeof(IActsOnBehalfOf).IsAssignableFrom(t)
                        && !typeof(IAuditedOnBehalfOf).IsAssignableFrom(t)
                        && !IntentionallyUnrestricted.Contains(t.Name))
            .Select(t => t.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            unaccounted.Count == 0,
            "Jelöletlen use case-ek — tedd rájuk az IRequireAdmin / IActsOnBehalfOf / IAuditedOnBehalfOf " +
            $"jelölőt, vagy vedd fel őket az IntentionallyUnrestricted listára: {string.Join(", ", unaccounted)}");
    }

    [Fact]
    public void The_unrestricted_allowlist_has_no_stale_entries()
    {
        var existingNames = AllRequestTypes().Select(t => t.Name).ToHashSet(StringComparer.Ordinal);
        var stale = IntentionallyUnrestricted.Where(n => !existingNames.Contains(n)).ToList();

        Assert.True(stale.Count == 0, $"Már nem létező use case a listán: {string.Join(", ", stale)}");
    }

    [Fact]
    public void No_use_case_carries_conflicting_markers()
    {
        // A jelölők egymást kizárják: az IRequireAdmin admint követel, az IActsOnBehalfOf a sajátján
        // engedi a nem-admint, az IAuditedOnBehalfOf pedig bárkit bárkin. Kettő együtt ellentmondás.
        var conflicting = AllRequestTypes()
            .Where(t => new[]
            {
                typeof(IRequireAdmin).IsAssignableFrom(t),
                typeof(IActsOnBehalfOf).IsAssignableFrom(t),
                typeof(IAuditedOnBehalfOf).IsAssignableFrom(t),
            }.Count(applies => applies) > 1)
            .Select(t => t.Name)
            .ToList();

        Assert.True(conflicting.Count == 0, $"Egyszerre több jelölőt visel: {string.Join(", ", conflicting)}");
    }

    [Fact]
    public void Ordering_on_someone_elses_behalf_stays_open_to_everyone()
    {
        // AC 3.1.6 / AC 9.2.2: a kollégának is le lehet adni a rendelését, a védelmet az audit adja.
        // Ha valaki ezekre IRequireAdmin-t tenne, azzal némán megszűnne egy szándékos funkció —
        // pontosan ez történt egyszer már. Ez a lista SZŰK: csak az van rajta, ami a leadáshoz kell.
        string[] mustStayOpen =
        [
            "PlacePeriodOrderCommand",
            "GetOrderableDaysQuery",
        ];

        var byName = AllRequestTypes().ToDictionary(t => t.Name, t => t);

        foreach (var name in mustStayOpen)
        {
            Assert.True(byName.ContainsKey(name), $"Nincs ilyen use case: {name}");
            Assert.True(
                typeof(IAuditedOnBehalfOf).IsAssignableFrom(byName[name]),
                $"{name} nem IAuditedOnBehalfOf — a más nevében rendelés bárkinek engedett (AC 9.2.2).");
        }
    }

    [Fact]
    public void Cancelling_and_reading_someone_elses_orders_require_admin()
    {
        // A más nevében rendelés párja NEM szimmetrikus: leadni szívesség, lemondani kárt okoz
        // (AC 3.2.8), a rendeléstörténet lekérdezése pedig épp az, amit a kolléga nem láthat
        // (AC 3.1.9). Mindkettő IActsOnBehalfOf: sajáton szabad, idegenen csak adminnak.
        string[] mustRequireAdminForOthers =
        [
            "CancelMenuOrdersCommand",
            "GetMyPeriodOrderQuery",
        ];

        var byName = AllRequestTypes().ToDictionary(t => t.Name, t => t);

        foreach (var name in mustRequireAdminForOthers)
        {
            Assert.True(byName.ContainsKey(name), $"Nincs ilyen use case: {name}");
            Assert.True(
                typeof(IActsOnBehalfOf).IsAssignableFrom(byName[name]),
                $"{name} nem IActsOnBehalfOf — idegen felhasználóra admin-jogot kell kívánnia.");
            Assert.False(
                typeof(IAuditedOnBehalfOf).IsAssignableFrom(byName[name]),
                $"{name} IAuditedOnBehalfOf lett — ezzel bárki elvégezhetné bárki nevében (AC 3.2.8 / 3.1.9).");
        }
    }
}
