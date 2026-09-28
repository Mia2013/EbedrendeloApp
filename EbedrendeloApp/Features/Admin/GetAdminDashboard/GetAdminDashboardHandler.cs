using EbedrendeloApp.Common.Calendar;
using EbedrendeloApp.Common.Services;
using EbedrendeloApp.Common.Time;
using EbedrendeloApp.Data;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Kitchen;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.Admin.GetAdminDashboard;

public sealed class GetAdminDashboardHandler(
    IDbContextFactory<EbedrendeloDbContext> dbFactory,
    IAppClock clock,
    IWorkingDayCalculator workingDayCalculator)
    : IRequestHandler<GetAdminDashboardQuery, AdminDashboardDto>
{
    /// <summary>Ha nincs a mai napot lefedő időszak, ennyi napra előre keressük a hiányzó menüket —
    /// enélkül nem lenne mihez viszonyítani a „menü nélküli munkanap" számot.</summary>
    private const int FallbackHorizonDays = 14;

    public async Task<AdminDashboardDto> Handle(GetAdminDashboardQuery request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var today = clock.Today;
        var localNow = clock.LocalNow;
        var settings = await db.AppSettings.FirstAsync(cancellationToken);

        // A diagramok a mai napot tartalmazó hét hétfő–péntekét mutatják, nem az utolsó öt napot: így
        // a hasáb helye a héten belül állandó, és a „Ma" oszlop nem vándorol nap mint nap.
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var friday = monday.AddDays(4);

        var period = await db.OrderingPeriods
            .Where(p => p.StartDate <= today && p.EndDate >= today)
            // Ha egy napot (átfedés-tiltás ide vagy oda) mégis két időszak fedne, a nyitott a
            // relevánsabb: arra lehet még rendelni.
            .OrderByDescending(p => p.IsOpen).ThenBy(p => p.StartDate)
            .Select(p => new { p.Id, p.Name, p.StartDate, p.EndDate, p.IsOpen })
            .FirstOrDefaultAsync(cancellationToken);

        var excludedDates = await db.ExcludedDays
            .Where(e => e.Date >= today)
            .Select(e => e.Date)
            .ToHashSetAsync(cancellationToken);

        var horizonEnd = period?.EndDate ?? today.AddDays(FallbackHorizonDays);

        var publishedMenuDates = await db.DailyMenus
            .Where(m => m.Date >= today && m.Date <= horizonEnd && m.IsPublished && m.RemovedAtUtc == null)
            .Select(m => m.Date)
            .ToHashSetAsync(cancellationToken);

        var missingMenuDates = WorkingDaysBetween(today, horizonEnd, excludedDates)
            .Where(date => !publishedMenuDates.Contains(date))
            .ToList();

        var activePeriod = period is null
            ? null
            : new AdminPeriodDto(
                period.Id,
                period.Name,
                period.StartDate,
                period.EndDate,
                period.IsOpen,
                WorkingDaysBetween(today, period.EndDate, excludedDates).Count);

        // A mai menü sorai ugyanabból a helperből jönnek, mint a konyhai összesítő — a nem rendelt
        // variáns is látszik, 0 adaggal.
        var liveVariants = await KitchenSummaryLines.LoadLiveVariantsAsync(db, today, today, cancellationToken);
        var orderedThisWeek = await KitchenSummaryLines.LoadOrderedVariantsAsync(db, monday, friday, cancellationToken);

        var todayVariants = KitchenSummaryLines
            .Build(liveVariants, orderedThisWeek.Where(o => o.Date == today))
            .Select(l => new AdminMenuVariantDto(l.VariantCode, l.VariantName, l.Quantity))
            .ToList();

        var weeklyMenuPortions = WorkingDaysBetween(monday, friday, ExcludedDates.None)
            .Select(date => new AdminDailyPortionsDto(
                date,
                orderedThisWeek.Where(o => o.Date == date).Sum(o => o.Quantity)))
            .ToList();

        var cancelledToday = await db.MenuOrders
            .CountAsync(o => o.Date == today && o.Status == OrderStatus.Cancelled, cancellationToken);

        var todayClosed = await KitchenClosureQueries.IsClosedAsync(db, today, cancellationToken);

        var unpaid = await db.PeriodInvoices
            // A jóváírással teljesen fedezett (0 Ft-os) számlát a generálás nem jelöli fizetettnek, de
            // nincs rajta mit behajtani — nem teendő, és nem kintlévőség.
            .Where(i => !i.IsPaid && i.PayableHuf > 0)
            .Select(i => i.PayableHuf)
            .ToListAsync(cancellationToken);

        var todayOffers = await db.ALaCarteDailyOffers
            .Where(o => o.Date == today)
            .OrderBy(o => o.ALaCarteItem!.Category).ThenBy(o => o.ALaCarteItem!.Name)
            .Select(o => new AdminALaCarteOfferDto(o.ALaCarteItem!.Name, o.OrderedCount))
            .ToListAsync(cancellationToken);

        var todayALaCarteItemCount = await db.ALaCarteOrderLines
            .CountAsync(l => l.ALaCarteOrder!.Date == today, cancellationToken);

        var todayALaCarteUserCount = await db.ALaCarteOrders
            .Where(o => o.Date == today)
            .Select(o => o.UserId)
            .Distinct()
            .CountAsync(cancellationToken);

        var nextWorkingDay = NextWorkingDay(today, excludedDates);
        var nextWorkingDayHasOffer = await db.ALaCarteDailyOffers
            .AnyAsync(o => o.Date == nextWorkingDay && o.Capacity > 0, cancellationToken);

        var weeklyALaCarte = await LoadWeeklyALaCarteAsync(db, monday, friday, cancellationToken);

        var tiles = await LoadTileStatsAsync(db, request.CurrentUserId, today, period?.Id, excludedDates.Count, cancellationToken);

        var todos = BuildTodos(
            missingMenuDates,
            todayClosed,
            todayVariants.Sum(v => v.Portions) + todayALaCarteItemCount,
            unpaid,
            nextWorkingDayHasOffer);

        return new AdminDashboardDto(
            today,
            activePeriod,
            todos,
            todayVariants.Sum(v => v.Portions),
            todayVariants,
            cancelledToday,
            missingMenuDates.Count,
            missingMenuDates.Count > 0 ? missingMenuDates[0] : null,
            todayClosed,
            unpaid.Count,
            unpaid.Sum(),
            weeklyMenuPortions,
            todayOffers,
            todayALaCarteItemCount,
            todayALaCarteUserCount,
            settings.ALaCarteOrderDeadlineLocalTime,
            localNow.TimeOfDay > settings.ALaCarteOrderDeadlineLocalTime.ToTimeSpan(),
            nextWorkingDayHasOffer,
            weeklyALaCarte,
            tiles);
    }

    private async Task<IReadOnlyList<AdminALaCarteDayDto>> LoadWeeklyALaCarteAsync(
        EbedrendeloDbContext db, DateOnly monday, DateOnly friday, CancellationToken cancellationToken)
    {
        var byDayAndCategory = await db.ALaCarteOrderLines
            .Where(l => l.ALaCarteOrder!.Date >= monday && l.ALaCarteOrder.Date <= friday)
            .GroupBy(l => new { l.ALaCarteOrder!.Date, l.CategorySnapshot })
            .Select(g => new { g.Key.Date, g.Key.CategorySnapshot, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return WorkingDaysBetween(monday, friday, ExcludedDates.None)
            .Select(date =>
            {
                var lines = byDayAndCategory.Where(x => x.Date == date).ToList();
                int CountOf(params ALaCarteCategory[] categories)
                    => lines.Where(x => categories.Contains(x.CategorySnapshot)).Sum(x => x.Count);

                return new AdminALaCarteDayDto(
                    date,
                    CountOf(ALaCarteCategory.Leves, ALaCarteCategory.Foetel),
                    CountOf(ALaCarteCategory.Koret, ALaCarteCategory.Ontet),
                    CountOf(ALaCarteCategory.Desszert));
            })
            .ToList();
    }

    private static async Task<AdminTileStatsDto> LoadTileStatsAsync(
        EbedrendeloDbContext db,
        int currentUserId,
        DateOnly today,
        int? activePeriodId,
        int futureExcludedDayCount,
        CancellationToken cancellationToken)
    {
        var openPeriodCount = await db.OrderingPeriods
            .CountAsync(p => p.IsOpen && p.EndDate >= today, cancellationToken);

        var activeOrderCountInPeriod = activePeriodId is null
            ? 0
            : await db.MenuOrders.CountAsync(
                o => o.OrderingPeriodId == activePeriodId && o.Status == OrderStatus.Active,
                cancellationToken);

        var activeALaCarteItemCount = await db.ALaCarteItems.CountAsync(i => i.IsActive, cancellationToken);

        var usersWithBalanceCount = await db.CreditEntries
            .GroupBy(c => c.UserId)
            .Select(g => new { UserId = g.Key, BalanceHuf = g.Sum(c => c.RemainingHuf) })
            .Where(x => x.BalanceHuf != 0)
            .CountAsync(cancellationToken);

        var myTodayVariantCode = await db.MenuOrders
            .Where(o => o.UserId == currentUserId && o.Date == today && o.Status == OrderStatus.Active)
            .Join(db.MenuVariants, o => o.MenuVariantId, v => v.Id, (_, v) => v.Code)
            .FirstOrDefaultAsync(cancellationToken);

        var myTodayALaCarteItemCount = await db.ALaCarteOrderLines
            .CountAsync(l => l.ALaCarteOrder!.Date == today && l.ALaCarteOrder.UserId == currentUserId, cancellationToken);

        return new AdminTileStatsDto(
            openPeriodCount,
            futureExcludedDayCount,
            activeOrderCountInPeriod,
            activeALaCarteItemCount,
            usersWithBalanceCount,
            myTodayVariantCode,
            myTodayALaCarteItemCount);
    }

    /// <summary>A teendők sorrendje szándékos: elöl az, ami miatt a dolgozók nem tudnak rendelni,
    /// hátul az, ami csak az adminnak feladat.</summary>
    private static IReadOnlyList<AdminTodoDto> BuildTodos(
        IReadOnlyList<DateOnly> missingMenuDates,
        bool todayClosed,
        int todayPortionsAndItems,
        IReadOnlyList<int> unpaidPayableHuf,
        bool nextWorkingDayHasALaCarteOffer)
    {
        var todos = new List<AdminTodoDto>();

        if (missingMenuDates.Count > 0)
        {
            todos.Add(new AdminTodoDto(AdminTodoKind.MissingDailyMenu, missingMenuDates.Count, 0, missingMenuDates));
        }

        if (!nextWorkingDayHasALaCarteOffer)
        {
            todos.Add(new AdminTodoDto(AdminTodoKind.MissingALaCarteOffer, 0, 0, []));
        }

        // Üres napot nincs értelme lezáratni: a teendő akkor teendő, ha van mit összesíteni.
        if (!todayClosed && todayPortionsAndItems > 0)
        {
            todos.Add(new AdminTodoDto(AdminTodoKind.KitchenDayOpen, todayPortionsAndItems, 0, []));
        }

        if (unpaidPayableHuf.Count > 0)
        {
            todos.Add(new AdminTodoDto(AdminTodoKind.UnpaidInvoices, unpaidPayableHuf.Count, unpaidPayableHuf.Sum(), []));
        }

        return todos;
    }

    private List<DateOnly> WorkingDaysBetween(DateOnly from, DateOnly to, IReadOnlySet<DateOnly> excludedDates)
    {
        var days = new List<DateOnly>();
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            if (workingDayCalculator.IsWorkingDay(date, excludedDates))
            {
                days.Add(date);
            }
        }

        return days;
    }

    private DateOnly NextWorkingDay(DateOnly today, IReadOnlySet<DateOnly> excludedDates)
    {
        var date = today.AddDays(1);
        // A hétvége plusz egy hosszabb kizárás sem tolhatja el két hétnél tovább — a védőkorlát csak
        // azért van, hogy egy elrontott kizárás-halmaz ne fagyassza be az oldalt.
        for (var i = 0; i < 14 && !workingDayCalculator.IsWorkingDay(date, excludedDates); i++)
        {
            date = date.AddDays(1);
        }

        return date;
    }
}
