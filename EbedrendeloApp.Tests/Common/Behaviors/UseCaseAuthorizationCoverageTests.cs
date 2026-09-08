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
                        && !IntentionallyUnrestricted.Contains(t.Name))
            .Select(t => t.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            unaccounted.Count == 0,
            "Jelöletlen use case-ek — tedd rájuk az IRequireAdmin vagy IActsOnBehalfOf jelölőt, vagy " +
            $"vedd fel őket az IntentionallyUnrestricted listára: {string.Join(", ", unaccounted)}");
    }

    [Fact]
    public void The_unrestricted_allowlist_has_no_stale_entries()
    {
        var existingNames = AllRequestTypes().Select(t => t.Name).ToHashSet(StringComparer.Ordinal);
        var stale = IntentionallyUnrestricted.Where(n => !existingNames.Contains(n)).ToList();

        Assert.True(stale.Count == 0, $"Már nem létező use case a listán: {string.Join(", ", stale)}");
    }

    [Fact]
    public void No_use_case_carries_both_markers()
    {
        // A kettő együtt ellentmondás: az IRequireAdmin úgyis admint követel, az IActsOnBehalfOf
        // pedig épp azt engedi meg, hogy a nem-admin a sajátján dolgozzon.
        var both = AllRequestTypes()
            .Where(t => typeof(IRequireAdmin).IsAssignableFrom(t) && typeof(IActsOnBehalfOf).IsAssignableFrom(t))
            .Select(t => t.Name)
            .ToList();

        Assert.True(both.Count == 0, $"Mindkét jelölőt viseli: {string.Join(", ", both)}");
    }
}
