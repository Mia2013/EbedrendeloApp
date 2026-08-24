using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.Menus.PublishDailyMenu;

public sealed class PublishDailyMenuHandler(IDbContextFactory<EbedrendeloDbContext> dbFactory)
    : IRequestHandler<PublishDailyMenuCommand, Result>
{
    public async Task<Result> Handle(PublishDailyMenuCommand request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var menu = await db.DailyMenus.FirstOrDefaultAsync(m => m.Date == request.Date && m.RemovedAtUtc == null, cancellationToken);
        if (menu is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "Erre a napra nincs menü.");
        }

        var hasVariants = await db.MenuVariants.AnyAsync(v => v.DailyMenuId == menu.Id && v.RemovedAtUtc == null, cancellationToken);
        if (!hasVariants)
        {
            return Result.Failure(ErrorCodes.NoVariants, "A menühöz nincs egyetlen variáns sem, nem publikálható.");
        }

        menu.IsPublished = true;
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
