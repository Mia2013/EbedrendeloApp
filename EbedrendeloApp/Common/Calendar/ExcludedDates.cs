namespace EbedrendeloApp.Common.Calendar;

/// <summary>
/// Közös, megosztott kizárt-nap halmazok az <see cref="IWorkingDayCalculator"/> hívásaihoz.
///
/// A naptár-számítások szándékosan tiszta függvények: a hívó tölti be a kizárt napokat és adja át
/// paraméterként (lásd <see cref="IWorkingDayCalculator"/>). Azok a handlerek viszont, amelyek csak a
/// hétvége-szabályt kérdezik le (a kizárt napokat külön, saját lekérdezéssel kezelik), üres halmazt
/// adnak át — ez a mező pont ezt az egy allokációt teszi közössé, hogy ne kelljen handlerenként újra
/// deklarálni (korábban öt példányban élt, két különböző néven: <c>EmptyExcludedSet</c> és
/// <c>ImmutableExcludedSet</c>).
/// </summary>
public static class ExcludedDates
{
    /// <summary>Üres, megosztható halmaz — a hétvége-szabály önmagában való ellenőrzéséhez.</summary>
    public static readonly IReadOnlySet<DateOnly> None = new HashSet<DateOnly>();
}
