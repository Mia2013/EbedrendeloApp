using EbedrendeloApp.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.Calendar.GetExcludedDays;

public sealed class GetExcludedDaysHandler(IDbContextFactory<EbedrendeloDbContext> dbFactory)
    : IRequestHandler<GetExcludedDaysQuery, IReadOnlyList<ExcludedDayDto>>
{
    public async Task<IReadOnlyList<ExcludedDayDto>> Handle(GetExcludedDaysQuery request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.ExcludedDays
            .Where(e => e.Date >= request.From && e.Date <= request.To)
            .OrderBy(e => e.Date)
            .Select(e => new ExcludedDayDto(
                e.Date,
                e.Reason,
                (e.CreatedByUser!.VezetekNev + " " + e.CreatedByUser.KeresztNev).Trim(),
                e.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }
}
