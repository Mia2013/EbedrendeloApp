using EbedrendeloApp.Common.ALaCarte;
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
    public async Task<AdminDashboardDto> Handle(GetAdminDashboardQuery request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Egyetlen óra-leolvasás: a „ma" és a határidő-összevetés nem kerülhet két külön napra éjfélkor.
        var localNow = clock.LocalNow;
        var today = DateOnly.FromDateTime(localNow);
        var settings = await db.AppSettings.FirstAsync(cancellationToken);

        // A diagram a mai napot tartalmazó hét hétfő–péntekét mutatja, nem az utolsó öt napot: így
        // a hasáb helye a héten belül állandó, és a „Ma" oszlop nem vándorol nap mint nap.
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var friday = monday.AddDays(4);

        var periods = await db.OrderingPeriods
            .Where(p => p.EndDate >= today)
            .Select(p => new { p.Id, p.Name, p.StartDate, p.EndDate, p.IsOpen })
            .ToListAsync(cancellationToken);

        var period = periods
            .Where(p => p.StartDate <= today)
            // Ha egy napot (átfedés-tiltás ide vagy oda) mégis két időszak fedne, a nyitott a
            // relevánsabb: arra lehet még rendelni.
            .OrderByDescending(p => p.IsOpen).ThenBy(p => p.StartDate)
            .FirstOrDefault();

        var excludedDates = await db.ExcludedDays
            .Where(e => e.Date >= today)
            .Select(e => e.Date)
            .ToHashSetAsync(cancellationToken);

        var todayIsServiceDay = workingDayCalculator.IsWorkingDay(today, excludedDates);

        // A hiányzó menüket mától MINDEN időszakban keressük, nem csak a maiban: a következő időszak
        // tömeges rendelési ablaka még a mostani alatt nyílik, és ha ott nincs menü, a dolgozók
        // rendelése napról napra elbukik. Időszakon kívüli napra menü úgysem vihető fel — azt nem számoljuk.
        var coveredWorkingDays = periods
            .SelectMany(p => WorkingDaysBetween(Max(today, p.StartDate), p.EndDate, excludedDates))
            .Distinct()
            .Order()
            .ToList();

        var horizonEnd = periods.Count > 0 ? periods.Max(p => p.EndDate) : today;
        var publishedMenuDates = await db.DailyMenus
            .Where(m => m.Date >= today && m.Date <= horizonEnd && m.IsPublished && m.RemovedAtUtc == null)
            .Select(m => m.Date)
            .ToHashSetAsync(cancellationToken);

        var missingMenuDates = coveredWorkingDays
            .Where(date => !publishedMenuDates.Contains(date))
            .ToList();

        var activePeriod = period is null
            ? null
            : new AdminPeriodDto(
                period.Name,
                period.StartDate,
                period.EndDate,
                period.IsOpen,
                WorkingDaysBetween(today, period.EndDate, excludedDates).Count);

        // A mai menü sorai ugyanabból a helperből jönnek, mint a konyhai összesítő — a nem rendelt
        // variáns is látszik, 0 adaggal.
        var liveVariants = await KitchenSummaryLines.LoadLiveVariantsAsync(db, today, today, cancellationToken);
        var orderedToday = await KitchenSummaryLines.LoadOrderedVariantsAsync(db, today, today, cancellationToken);
        var todayVariants = KitchenSummaryLines
            .Build(liveVariants, orderedToday)
            .Select(l => new AdminMenuVariantDto(l.VariantCode, l.VariantName, l.Quantity))
            .ToList();
        var todayPortions = todayVariants.Sum(v => v.Portions);

        // A variáns-csere egy lemondás + egy új rendelés; az nem kiesett adag. Egy felhasználónak
        // naponta legfeljebb egy aktív rendelése van, ezért a „nem pótolt lemondás" = a felhasználók
        // száma, akiknek van lemondott, de nincs aktív rendelésük mára.
        var cancelledToday = await db.MenuOrders
            .Where(o => o.Date == today && o.Status == OrderStatus.Cancelled
                        && !db.MenuOrders.Any(a => a.UserId == o.UserId && a.Date == today && a.Status == OrderStatus.Active))
            .Select(o => o.UserId)
            .Distinct()
            .CountAsync(cancellationToken);

        var todayClosed = await KitchenClosureQueries.IsClosedAsync(db, today, cancellationToken);

        var unpaidInvoices = db.PeriodInvoices.Where(i => !i.IsPaid);
        var unpaidCount = await unpaidInvoices.CountAsync(cancellationToken);
        var unpaidTotalHuf = await unpaidInvoices.SumAsync(i => i.PayableHuf, cancellationToken);

        var todayOffers = await LoadTodayOffersAsync(db, today, cancellationToken);

        var todayALaCarteItemCount = await db.ALaCarteOrderLines
            .CountAsync(l => l.ALaCarteOrder!.Date == today, cancellationToken);

        // A sorokon át számolunk, nem a fejlécen: a tétel visszavonása csak a sort törli, a fejléc
        // marad — aki mindent visszamondott, az már nem rendelő.
        var todayALaCarteUserCount = await db.ALaCarteOrderLines
            .Where(l => l.ALaCarteOrder!.Date == today)
            .Select(l => l.ALaCarteOrder!.UserId)
            .Distinct()
            .CountAsync(cancellationToken);

        var nextALaCarteDay = NextCoveredWorkingDay(today, horizonEnd, excludedDates,
            date => periods.Any(p => p.StartDate <= date && p.EndDate >= date));

        // A leves önállóan nem rendelhető, a nullázott tétel pedig már nincs kínálatban — egyik sem
        // teszi rendelhetővé a napot.
        var nextALaCarteDayHasOffer = nextALaCarteDay is { } nextDay
            && await db.ALaCarteDailyOffers.AnyAsync(
                o => o.Date == nextDay && o.Capacity > 0 && o.ALaCarteItem!.Category != ALaCarteCategory.Leves,
                cancellationToken);

        var weeklyALaCarte = await LoadWeeklyALaCarteAsync(db, monday, friday, cancellationToken);

        var tiles = await LoadTileStatsAsync(db, request.CurrentUserId, today, period?.Id, excludedDates.Count, cancellationToken);

        var todos = BuildTodos(
            missingMenuDates,
            todayClosed,
            todayPortions,
            unpaidCount,
            unpaidTotalHuf,
            nextALaCarteDay,
            nextALaCarteDayHasOffer);

        return new AdminDashboardDto(
            today,
            todayIsServiceDay,
            activePeriod,
            todos,
            todayPortions,
            todayVariants,
            cancelledToday,
            missingMenuDates.Count,
            missingMenuDates.Count > 0 ? missingMenuDates[0] : null,
            todayClosed,
            unpaidCount,
            unpaidTotalHuf,
            todayOffers,
            todayALaCarteItemCount,
            todayALaCarteUserCount,
            settings.ALaCarteOrderDeadlineLocalTime,
            ALaCarteOrderingGate.IsPastDeadline(settings, localNow),
            nextALaCarteDay,
            nextALaCarteDayHasOffer,
            weeklyALaCarte,
            tiles);
    }

    /// <summary>A mai kínálat sorai. A leves <c>OrderedCount</c>-ja sosem nő (a leves a főétel-sorokon
    /// utazik), ezért a levesadag a mai főétel-sorok száma — ugyanaz a szabály, mint a konyhai listán
    /// (AC 4.6.3). A nullázott, rendelés nélküli ajánlat már nincs kínálatban, nem listázzuk.</summary>
    private static async Task<IReadOnlyList<AdminALaCarteOfferDto>> LoadTodayOffersAsync(
        EbedrendeloDbContext db, DateOnly today, CancellationToken cancellationToken)
    {
        var soupPortions = await db.ALaCarteOrderLines
            .CountAsync(l => l.ALaCarteOrder!.Date == today && l.CategorySnapshot == ALaCarteCategory.Foetel, cancellationToken);

        var offers = await db.ALaCarteDailyOffers
            .Where(o => o.Date == today && (o.Capacity > 0 || o.OrderedCount > 0))
            .OrderBy(o => o.ALaCarteItem!.Category).ThenBy(o => o.ALaCarteItem!.Name)
            .Select(o => new { o.ALaCarteItem!.Name, o.ALaCarteItem.Category, o.OrderedCount })
            .ToListAsync(cancellationToken);

        return offers
            .Select(o => new AdminALaCarteOfferDto(o.Name, o.Category == ALaCarteCategory.Leves ? soupPortions : o.OrderedCount))
            .ToList();
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
        int todayPortions,
        int unpaidCount,
        int unpaidTotalHuf,
        DateOnly? nextALaCarteDay,
        bool nextALaCarteDayHasOffer)
    {
        var todos = new List<AdminTodoDto>();

        if (missingMenuDates.Count > 0)
        {
            todos.Add(new AdminTodoDto(AdminTodoKind.MissingDailyMenu, missingMenuDates.Count, 0, missingMenuDates));
        }

        if (nextALaCarteDay is { } nextDay && !nextALaCarteDayHasOffer)
        {
            todos.Add(new AdminTodoDto(AdminTodoKind.MissingALaCarteOffer, 0, 0, [nextDay]));
        }

        // Üres napot nincs értelme lezáratni: a teendő akkor teendő, ha van menüadag, amit összesíteni kell.
        if (!todayClosed && todayPortions > 0)
        {
            todos.Add(new AdminTodoDto(AdminTodoKind.KitchenDayOpen, todayPortions, 0, []));
        }

        if (unpaidCount > 0)
        {
            todos.Add(new AdminTodoDto(AdminTodoKind.UnpaidInvoices, unpaidCount, unpaidTotalHuf, []));
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

    /// <summary>A mai nap utáni első munkanap, amit időszak fed — addig keresünk, ameddig időszak
    /// egyáltalán van (<paramref name="horizonEnd"/>), így hosszú kizárás sem tolja hétvégére.</summary>
    private DateOnly? NextCoveredWorkingDay(
        DateOnly today, DateOnly horizonEnd, IReadOnlySet<DateOnly> excludedDates, Func<DateOnly, bool> isCovered)
    {
        for (var date = today.AddDays(1); date <= horizonEnd; date = date.AddDays(1))
        {
            if (workingDayCalculator.IsWorkingDay(date, excludedDates) && isCovered(date))
            {
                return date;
            }
        }

        return null;
    }

    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;
}
