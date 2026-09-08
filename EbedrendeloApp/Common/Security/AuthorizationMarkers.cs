namespace EbedrendeloApp.Common.Security;

/// <summary>
/// Jelölő interfész: a kérés csak adminisztrátornak engedélyezett. Az ellenőrzést az
/// <c>AuthorizationBehavior</c> végzi a MediatR pipeline-ban, tehát a szerveren, minden hívási úton —
/// nem a Razor oldal <c>OnInitializedAsync</c>-jében lévő átirányítás, ami csak a felület kényelme.
/// </summary>
public interface IRequireAdmin;

/// <summary>
/// Jelölő interfész: a kérés egy konkrét felhasználó adatára/nevében történik. Idegen
/// <see cref="TargetUserId"/> csak adminisztrátornak engedélyezett — enélkül bárki rendelhetne,
/// mondhatna le és nézhetne egyenleget bárki nevében.
///
/// A parancsok mezőneve eltér (<c>UserId</c> / <c>TargetUserId</c>), ezért a record explicit
/// implementációval képezi le a sajátját erre.
/// </summary>
public interface IActsOnBehalfOf
{
    int TargetUserId { get; }
}

/// <summary>
/// Jelölő interfész: a kérés **szándékosan bárkinek engedett bárki nevében** — a védelmet az audit
/// adja (`PlacedByUserId` / `CancelledByUserId`), nem a jogosultság. Ez a menürendelés dokumentált
/// döntése (AC 3.1.6 és AC 9.2.2): a kollégának is le lehet adni a rendelését.
///
/// A „bárki bárkinek" nem jelent névsor-böngészést: a címzettet a dolgozónak azonosítania kell
/// (név + igazgatóság + osztály, <c>ResolveColleagueQuery</c>), a felület nem kínál végiglapozható
/// listát. Ez a jelölő tehát nem „nem gondoltunk rá", hanem „végiggondoltuk, és nyitva marad" —
/// ezért van saját interfésze ahelyett, hogy a jelöletlenek közé csúszna.
/// </summary>
public interface IAuditedOnBehalfOf;

/// <summary>
/// Jogosultsági sértés. Szándékosan kivétel és nem <c>Result.Failure</c>: nem üzleti kimenet, amit a
/// felületnek meg kellene jelenítenie, hanem hiba vagy visszaélés — ugyanaz az elv, mint a
/// <c>ValidationBehavior</c>-nál (NFR-2). A <c>MainLayout</c> <c>ErrorBoundary</c>-ja kezeli.
/// </summary>
public sealed class ForbiddenException(string message) : Exception(message);
