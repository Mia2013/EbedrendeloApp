using EbedrendeloApp.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.Menus.GetDailyMenu;

public sealed class GetDailyMenuHandler(IDbContextFactory<EbedrendeloDbContext> dbFactory)
    : IRequestHandler<GetDailyMenuQuery, DailyMenuDto?>
{
    public async Task<DailyMenuDto?> Handle(GetDailyMenuQuery request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var menu = await db.DailyMenus
            .Include(m => m.Variants.Where(v => v.RemovedAtUtc == null))
            .FirstOrDefaultAsync(m => m.Date == request.Date && m.RemovedAtUtc == null, cancellationToken);

        if (menu is null || (!menu.IsPublished && !request.IncludeUnpublished))
        {
            return null;
        }

        return new DailyMenuDto(
            menu.Date,
            menu.IsPublished,
            menu.Note,
            menu.Variants
                .OrderBy(v => v.SortOrder).ThenBy(v => v.Code, StringComparer.Ordinal)
                .Select(v => new MenuVariantDto(v.Code, v.Name, v.Description, v.SortOrder))
                .ToList());
    }
}
