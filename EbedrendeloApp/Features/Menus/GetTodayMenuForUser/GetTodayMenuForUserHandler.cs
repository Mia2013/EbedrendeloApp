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

        var menuPublished = menu is not null && menu.IsPublished;

        var variants = new List<MenuVariantDto>();
        MyMenuSelectionDto? mySelection = null;

        // Az à la carte kínálat a napi A/B/C menü publikálási állapotától függetlenül megjelenik
        // (AC 4.2.6) — ezért itt nincs korai return: a nem publikált/hiányzó menü csak a menüre
        // vonatkozó mezőket üresíti ki, az à la carte rész alább mindig kiszámolódik.
        if (menuPublished)
        {
            var dishes = await MenuDishAllergenLookup.LoadAsync(db, cancellationToken);

            variants = menu!.Variants
                .OrderBy(v => v.SortOrder).ThenBy(v => v.Code, StringComparer.Ordinal)
                .Select(v => MenuVariantDtoFactory.Create(v, dishes))
                .ToList();

            var myOrder = await db.MenuOrders
                .FirstOrDefaultAsync(o => o.UserId == request.UserId && o.Date == today && o.Status == OrderStatus.Active, cancellationToken);

            if (myOrder is not null)
            {
                var variant = menu.Variants.FirstOrDefault(v => v.Id == myOrder.MenuVariantId)
                    ?? await db.MenuVariants.FirstAsync(v => v.Id == myOrder.MenuVariantId, cancellationToken);
                mySelection = new MyMenuSelectionDto(variant.Code, variant.SoupName, myOrder.PriceHuf);
            }
        }

        var offers = await db.ALaCarteDailyOffers
            .Include(o => o.ALaCarteItem)
            .Where(o => o.Date == today)
            .ToListAsync(cancellationToken);

        // A leves önálló sorként sosem jelenik meg (AC 4.5.1) — az ára minden Főétel-ajánlat árába
        // beleolvad (AC 4.2.8). Legfeljebb egy aktív Leves-ajánlat lehet naponta (AC 4.1.4). Ha ma
        // nincs Leves-ajánlat, a Főétel ára a puszta katalógusár (0 Ft leves-rész, nem hibaeset).
        var todaySoupItem = offers.FirstOrDefault(o => o.ALaCarteItem!.Category == ALaCarteCategory.Leves)?.ALaCarteItem;

        var offerDtos = offers
            .Where(o => o.ALaCarteItem!.Category != ALaCarteCategory.Leves)
            .Select(o =>
            {
                var item = o.ALaCarteItem!;
                var includesSoup = item.Category == ALaCarteCategory.Foetel && todaySoupItem is not null;
                var priceHuf = includesSoup ? item.PriceHuf + todaySoupItem!.PriceHuf : item.PriceHuf;
                return new ALaCarteOfferDto(
                    o.ALaCarteItemId, item.Name, item.Category, priceHuf, o.Capacity - o.OrderedCount, item.Allergens, includesSoup,
                    item.EnergyKcal, item.FatGrams, item.SaturatedFatGrams, item.CarbohydrateGrams, item.SugarGrams, item.ProteinGrams, item.SaltGrams);
            })
            .OrderBy(o => o.Category).ThenBy(o => o.Name, StringComparer.Ordinal)
            .ToList();

        var myALaCarteOrder = await db.ALaCarteOrders
            .Include(o => o.Lines).ThenInclude(l => l.ALaCarteDailyOffer)
            .FirstOrDefaultAsync(o => o.UserId == request.UserId && o.Date == today, cancellationToken);

        var myLines = myALaCarteOrder?.Lines
            .Select(l => new MyALaCarteLineDto(l.ALaCarteDailyOffer!.ALaCarteItemId, l.ItemNameSnapshot, l.CategorySnapshot, l.UnitPriceHuf, l.IncludesSoup))
            .ToList() ?? [];

        var settings = await db.AppSettings.FirstAsync(cancellationToken);
        var isALaCarteOrderableNow = clock.LocalNow.TimeOfDay <= settings.ALaCarteOrderDeadlineLocalTime.ToTimeSpan();

        return new TodayMenuDto(
            today, menuPublished, menuPublished ? null : ErrorCodes.MenuNotPublished, variants, mySelection, offerDtos, myLines,
            settings.ALaCarteOrderDeadlineLocalTime, isALaCarteOrderableNow);
    }

    private static TodayMenuDto NotOrderable(DateOnly today, string reason) => new(today, false, reason, [], null, [], []);
}
