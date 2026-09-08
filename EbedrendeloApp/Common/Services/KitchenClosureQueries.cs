using EbedrendeloApp.Data;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Common.Services;

/// <summary>
/// Single source of truth for "is this day currently closed" (01-szerver-architektura.md 3.1/3.6, US-6.2).
/// A day is closed iff a <see cref="Domain.Entities.KitchenClosure"/> exists for it with no matching
/// <see cref="Domain.Entities.KitchenClosureReopening"/> — every gate-check handler across
/// Orders/Calendar/Menus must go through here rather than re-deriving this condition, so a future schema
/// change to what "closed" means only has to be made in one place.
/// </summary>
public static class KitchenClosureQueries
{
    public static Task<bool> IsClosedAsync(EbedrendeloDbContext db, DateOnly date, CancellationToken cancellationToken)
        => db.KitchenClosures.AnyAsync(k => k.Date == date && k.Reopening == null, cancellationToken);

    public static Task<HashSet<DateOnly>> GetClosedDatesAsync(
        EbedrendeloDbContext db, DateOnly from, DateOnly to, CancellationToken cancellationToken)
        => db.KitchenClosures
            .Where(k => k.Date >= from && k.Date <= to && k.Reopening == null)
            .Select(k => k.Date)
            .ToHashSetAsync(cancellationToken);
}
