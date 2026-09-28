using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Features.Users.GetUsers;
using MediatR;

namespace EbedrendeloApp.Features.Users.ResolveColleague;

/// <summary>
/// Egy kolléga azonosítása névvel + igazgatósággal + osztállyal, hogy a nevében rendelni lehessen
/// (AC 3.1.6). **Csak pontos egyezésre** ad találatot: nincs részleges keresés, nincs böngészhető
/// névsor, és a nem-találat nem árulja el, melyik mező volt hibás — így a hármas ismerete tényleges
/// feltétel, nem pedig kitalálható.
///
/// Szándékosan nincs <c>IRequireAdmin</c>: a dolgozónak is kell (a teljes névsort adó
/// <c>GetUsersQuery</c> viszont admin-only marad, hogy ne lehessen körbelapozni).
/// </summary>
public sealed record ResolveColleagueQuery(string Name, string Igazgatosag, string Osztaly)
    : IRequest<Result<UserOptionDto>>;
