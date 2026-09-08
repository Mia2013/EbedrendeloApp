using EbedrendeloApp.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.Kitchen.GetKitchenClosure;

public sealed class GetKitchenClosureHandler(IDbContextFactory<EbedrendeloDbContext> dbFactory)
    : IRequestHandler<GetKitchenClosureQuery, KitchenClosureDto?>
{
    public async Task<KitchenClosureDto?> Handle(GetKitchenClosureQuery request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // AC 6.3.2: a day can have been closed more than once over its lifetime (close → reopen →
        // close again) — the most recent closure is the one that matters for "what went out to the
        // kitchen last".
        var closure = await db.KitchenClosures
            .Include(k => k.Lines)
            .Include(k => k.Reopening)
            .Where(k => k.Date == request.Date)
            .OrderByDescending(k => k.ClosedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (closure is null)
        {
            return null;
        }

        var userIds = new List<int> { closure.ClosedByUserId };
        if (closure.Reopening is { } reopening)
        {
            userIds.Add(reopening.ReopenedByUserId);
        }

        var users = await db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, cancellationToken);

        string DisplayName(int userId) =>
            users.TryGetValue(userId, out var u) ? $"{u.VezetekNev} {u.KeresztNev}".Trim() : "Ismeretlen felhasználó";

        var lines = closure.Lines
            .Select(l => new KitchenClosureLineDto(l.VariantCode, l.VariantNameSnapshot, l.Quantity))
            .OrderBy(l => l.VariantCode, StringComparer.Ordinal)
            .ToList();

        return new KitchenClosureDto(
            closure.Date,
            closure.ClosedAtUtc,
            closure.ClosedByUserId,
            DisplayName(closure.ClosedByUserId),
            closure.TotalPortions,
            lines,
            closure.Reopening?.ReopenedAtUtc,
            closure.Reopening?.ReopenedByUserId,
            closure.Reopening is null ? null : DisplayName(closure.Reopening.ReopenedByUserId));
    }
}
