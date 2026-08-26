using EbedrendeloApp.Common.Calendar;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Services;
using EbedrendeloApp.Common.Time;
using EbedrendeloApp.Data;
using EbedrendeloApp.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.Menus.GetTodayMenuForUser;

public sealed class GetTodayMenuForUserHandler(
    IDbContextFactory<EbedrendeloDbContext> dbFactory,
    IAppClock clock,
    IWorkingDayCalculator workingDayCalculator)
    : IRequestHandler<GetTodayMenuForUserQuery, TodayMenuDto>
{
    private static readonly IReadOnlySet<DateOnly> EmptyExcludedSet = new HashSet<DateOnly>();

    public async Task<TodayMenuDto> Handle(GetTodayMenuForUserQuery request, CancellationToken cancellationToken)
    {
        var today = clock.Today;

        if (!workingDayCalculator.IsWorkingDay(today, EmptyExcludedSet))
        {
            return NotOrderable(today, ErrorCodes.NotWorkingDay);
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        if (await db.ExcludedDays.AnyAsync(e => e.Date == today, cancellationToken))
        {
            return NotOrderable(today, ErrorCodes.DayExcluded);
        }

        var menu = await db.DailyMenus
            .Include(m => m.Variants.Where(v => v.RemovedAtUtc == null))
            .FirstOrDefaultAsync(m => m.Date == today && m.RemovedAtUtc == null, cancellationToken);

        if (menu is null || !menu.IsPublished)
        {
            return NotOrderable(today, ErrorCodes.MenuNotPublished);
        }

        var dishes = await MenuDishAllergenLookup.LoadAsync(db, cancellationToken);

        var variants = menu.Variants
            .OrderBy(v => v.SortOrder).ThenBy(v => v.Code, StringComparer.Ordinal)
            .Select(v => MenuVariantDtoFactory.Create(v, dishes))
            .ToList();

        var myOrder = await db.MenuOrders
            .FirstOrDefaultAsync(o => o.UserId == request.UserId && o.Date == today && o.Status == OrderStatus.Active, cancellationToken);

        MyMenuSelectionDto? mySelection = null;
        if (myOrder is not null)
        {
            var variant = menu.Variants.FirstOrDefault(v => v.Id == myOrder.MenuVariantId)
                ?? await db.MenuVariants.FirstAsync(v => v.Id == myOrder.MenuVariantId, cancellationToken);
            mySelection = new MyMenuSelectionDto(variant.Code, variant.Name, myOrder.PriceHuf);
        }

        var offers = await db.ALaCarteDailyOffers
            .Include(o => o.ALaCarteItem)
            .Where(o => o.Date == today)
            .ToListAsync(cancellationToken);

        var offerDtos = offers
            .Select(o => new ALaCarteOfferDto(o.ALaCarteItemId, o.ALaCarteItem!.Name, o.ALaCarteItem.Category, o.ALaCarteItem.PriceHuf, o.Capacity - o.OrderedCount, o.ALaCarteItem.Allergens))
            .OrderBy(o => o.Category).ThenBy(o => o.Name, StringComparer.Ordinal)
            .ToList();

        var myALaCarteOrder = await db.ALaCarteOrders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.UserId == request.UserId && o.Date == today, cancellationToken);

        var myLines = myALaCarteOrder?.Lines
            .Select(l => new MyALaCarteLineDto(l.ItemNameSnapshot, l.CategorySnapshot, l.UnitPriceHuf))
            .ToList() ?? [];

        return new TodayMenuDto(today, true, null, variants, mySelection, offerDtos, myLines);
    }

    private static TodayMenuDto NotOrderable(DateOnly today, string reason) => new(today, false, reason, [], null, [], []);
}
