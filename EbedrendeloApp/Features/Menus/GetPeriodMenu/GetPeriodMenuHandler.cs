using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Common.Services;
using EbedrendeloApp.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.Menus.GetPeriodMenu;

public sealed class GetPeriodMenuHandler(
    IDbContextFactory<EbedrendeloDbContext> dbFactory,
    ICurrentUser currentUser)
    : IRequestHandler<GetPeriodMenuQuery, Result<IReadOnlyList<DailyMenuDto>>>
{
    public async Task<Result<IReadOnlyList<DailyMenuDto>>> Handle(GetPeriodMenuQuery request, CancellationToken cancellationToken)
    {
        // A kérés maga nyitott (a dolgozói naptár is hívja), de a nem publikált napok admin-adatok
        // (AC 2.5.2). Jelölővel ezt nem lehetne kifejezni, mert nem a kérés, hanem egy paraméterérték
        // igényel jogot — ezért itt, a handlerben van a kapu.
        if (request.IncludeUnpublished)
        {
            await currentUser.EnsureLoadedAsync(cancellationToken);
            if (!currentUser.IsAdmin)
            {
                throw new ForbiddenException("A nem publikált menük megtekintéséhez adminisztrátori jogosultság szükséges.");
            }
        }

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
        var dishes = await MenuDishAllergenLookup.LoadAsync(db, cancellationToken);

        var dtos = menus
            .Select(m => new DailyMenuDto(
                m.Date,
                m.IsPublished,
                m.Note,
                m.Variants
                    .OrderBy(v => v.SortOrder).ThenBy(v => v.Code, StringComparer.Ordinal)
                    .Select(v => MenuVariantDtoFactory.Create(v, dishes))
                    .ToList()))
            .ToList();

        return Result.Success<IReadOnlyList<DailyMenuDto>>(dtos);
    }
}
