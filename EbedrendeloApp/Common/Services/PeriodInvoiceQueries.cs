using EbedrendeloApp.Data;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Common.Services;

/// <summary>
/// Single source of truth for "is this user already invoiced for this period" (01-szerver-architektura.md
/// 3.3, Epic 7 AC 7.1.6) — every gate-check across Orders/Calendar must go through here rather than
/// re-deriving the condition, mirroring <see cref="KitchenClosureQueries"/>.
/// </summary>
public static class PeriodInvoiceQueries
{
    public static async Task<HashSet<(int UserId, int OrderingPeriodId)>> GetInvoicedUserPeriodsAsync(
        EbedrendeloDbContext db, IEnumerable<int> userIds, IEnumerable<int> periodIds, CancellationToken cancellationToken)
    {
        var userIdList = userIds.Distinct().ToList();
        var periodIdList = periodIds.Distinct().ToList();

        return (await db.PeriodInvoices
                .Where(i => userIdList.Contains(i.UserId) && periodIdList.Contains(i.OrderingPeriodId))
                .Select(i => new { i.UserId, i.OrderingPeriodId })
                .ToListAsync(cancellationToken))
            .Select(i => (i.UserId, i.OrderingPeriodId))
            .ToHashSet();
    }
}
