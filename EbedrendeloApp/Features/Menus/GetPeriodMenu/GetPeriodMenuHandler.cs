using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.Menus.GetPeriodMenu;

public sealed class GetPeriodMenuHandler(IDbContextFactory<EbedrendeloDbContext> dbFactory)
    : IRequestHandler<GetPeriodMenuQuery, Result<IReadOnlyList<DailyMenuDto>>>
{
    public async Task<Result<IReadOnlyList<DailyMenuDto>>> Handle(GetPeriodMenuQuery request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var period = await db.OrderingPeriods.FirstOrDefaultAsync(p => p.Id == request.OrderingPeriodId, cancellationToken);
        if (period is null)
        {
            return Result.Failure<IReadOnlyList<DailyMenuDto>>(ErrorCodes.NotFound, "Az időszak nem található.");
        }

        var query = db.DailyMenus
            .Include(m => m.Variants.Where(v => v.RemovedAtUtc == null))
            .Where(m => m.Date >= period.StartDate && m.Date <= period.EndDate && m.RemovedAtUtc == null);

        if (!request.IncludeUnpublished)
        {
            query = query.Where(m => m.IsPublished);
        }

        var menus = await query.OrderBy(m => m.Date).ToListAsync(cancellationToken);

        var dtos = menus
            .Select(m => new DailyMenuDto(
                m.Date,
                m.IsPublished,
                m.Note,
                m.Variants
                    .OrderBy(v => v.SortOrder).ThenBy(v => v.Code, StringComparer.Ordinal)
                    .Select(v => new MenuVariantDto(v.Code, v.Name, v.Description, v.SortOrder))
                    .ToList()))
            .ToList();

        return Result.Success<IReadOnlyList<DailyMenuDto>>(dtos);
    }
}
