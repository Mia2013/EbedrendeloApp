using EbedrendeloApp.Common.Calendar;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Data;
using EbedrendeloApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Common.ALaCarte;

/// <summary>
/// Az à la carte rendelés (<c>PlaceALaCarteOrderHandler</c>) és a rendelés-visszavonás
/// (<c>CancelALaCarteOrderLineHandler</c>) közös napi kapu-ellenőrzései: ugyanaz a hétvége-, kizárt
/// nap- és határidő-szabály vonatkozik mindkettőre (AC 4.1.3/4.2.5/4.2.7), ezért a szabály — a
/// hozzá tartozó hibakóddal és üzenettel együtt — itt él egy példányban, nem handlerenként másolva.
///
/// A metódusok szándékosan külön hívhatók, nem egyetlen „ellenőrizz mindent" blokként:
/// <list type="bullet">
/// <item>a hétvége-ellenőrzés adatbázis nélkül fut, ezért a hívók még a DbContext megnyitása előtt
/// elvégzik (hétvégén így egy kapcsolat sem nyílik);</item>
/// <item>a határidő-ellenőrzés helye handleren belül eltér: rendeléskor rögtön a nap-ellenőrzések
/// után jön, visszavonáskor viszont csak a tétel megtalálása után (hogy egy soha meg nem rendelt
/// tételre a félrevezető „lejárt a határidő" helyett <see cref="ErrorCodes.NotFound"/> jöjjön).</item>
/// </list>
/// </summary>
public static class ALaCarteOrderingGate
{
    /// <summary>Hétvégén nincs kiszolgálás (a kizárt napokat lásd
    /// <see cref="CheckDayNotExcludedAsync"/>) — adatbázis-hozzáférés nélkül eldönthető.</summary>
    public static Result CheckWorkingDay(DateOnly today, IWorkingDayCalculator workingDayCalculator)
        => workingDayCalculator.IsWorkingDay(today, ExcludedDates.None)
            ? Result.Success()
            : Result.Failure(ErrorCodes.NotWorkingDay, "Ma nem munkanap.");

    /// <summary>Az adott napra felvett kizárás (pl. konyhai leállás) esetén nincs à la carte
    /// kiszolgálás.</summary>
    public static async Task<Result> CheckDayNotExcludedAsync(
        EbedrendeloDbContext db, DateOnly today, CancellationToken cancellationToken)
        => await db.ExcludedDays.AnyAsync(e => e.Date == today, cancellationToken)
            ? Result.Failure(ErrorCodes.DayExcluded, "Erre a napra nincs rendelés.")
            : Result.Success();

    /// <summary>A napi à la carte határidő (<see cref="AppSetting.ALaCarteOrderDeadlineLocalTime"/>)
    /// lejárt-e. A hibaüzenetet a hívó adja, mert rendeléskor és visszavonáskor más a megfogalmazás.</summary>
    public static bool IsPastDeadline(AppSetting settings, DateTime localNow)
        => localNow.TimeOfDay > settings.ALaCarteOrderDeadlineLocalTime.ToTimeSpan();
}
