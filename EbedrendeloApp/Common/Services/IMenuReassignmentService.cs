using EbedrendeloApp.Data;
using EbedrendeloApp.Domain.Entities;

namespace EbedrendeloApp.Common.Services;

/// <summary>
/// Moves active orders off a menu variant that is being removed — either to the day's first
/// remaining variant (by SortOrder, then Code), or cancels them with a refund if none remain
/// (01-szerver-architektura.md 3.2). Operates on the caller's <see cref="EbedrendeloDbContext"/> and
/// does not call SaveChanges — the handler owns the unit of work, same convention as
/// <see cref="ICreditService"/>. The caller is responsible for soft-deleting
/// <paramref name="removedVariant"/> itself afterward — this service only touches orders.
/// </summary>
public interface IMenuReassignmentService
{
    /// <returns>The ids of the <see cref="MenuOrder"/> rows this call touched (reassigned or cancelled).</returns>
    Task<IReadOnlyList<int>> ReassignOrCancelAsync(
        EbedrendeloDbContext db,
        DateOnly date,
        MenuVariant removedVariant,
        IReadOnlyList<MenuVariant> remainingVariants,
        int performedByUserId,
        DateTime nowUtc,
        CancellationToken cancellationToken);
}
