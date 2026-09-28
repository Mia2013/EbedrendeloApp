using EbedrendeloApp.Common.Security;
using MediatR;

namespace EbedrendeloApp.Features.Admin.GetAdminDashboard;

/// <summary>
/// Az admin áttekintő oldal (<c>/admin</c>) egyetlen lekérdezése. Szándékosan egy hívás és nem
/// nyolc: az oldal minden doboza ugyanarra a mai napra és ugyanarra az aktív időszakra vonatkozik,
/// külön lekérdezésekkel viszont a nyolc válasz különböző pillanatképeket látna (a nap éjfélkor
/// fordulhat, a konyha közben zárhat), és a kártyák ellentmondanának egymásnak.
///
/// Nincs validátora: a <paramref name="CurrentUserId"/> a bejelentkezett admin saját azonosítója, és
/// nincs rajta formai szabály — a jogosultságot az <see cref="IRequireAdmin"/> jelölő intézi.
/// Nincs <c>Result</c> burka sem: az oldal minden ága kirajzolható üres adatból is (nincs aktív
/// időszak, nincs mai menü), tehát nincs üzleti hiba, amit jelezni kellene.
/// </summary>
public sealed record GetAdminDashboardQuery(int CurrentUserId) : IRequest<AdminDashboardDto>, IRequireAdmin;

/// <summary>Ami ma az adminra vár. A megjelenítést (ikon, szín, cél-oldal, szöveg) a
/// <c>Common/Admin/AdminTodoDisplay</c> képezi le, hogy a handler adatot adjon vissza, ne feliratot.</summary>
public enum AdminTodoKind
{
    /// <summary>Van olyan közelgő munkanap, amire nincs publikált napi menü.</summary>
    MissingDailyMenu,

    /// <summary>A mai napot még nem zárta le a konyha, pedig van mit összesíteni.</summary>
    KitchenDayOpen,

    /// <summary>Van kiállított, de kifizetetlen számla.</summary>
    UnpaidInvoices,

    /// <summary>A következő munkanapra nincs egyetlen à la carte tétel sem kiajánlva.</summary>
    MissingALaCarteOffer,
}

/// <summary><paramref name="Dates"/> csak a <see cref="AdminTodoKind.MissingDailyMenu"/> ágon van
/// kitöltve, <paramref name="AmountHuf"/> csak a számlásán.</summary>
public sealed record AdminTodoDto(AdminTodoKind Kind, int Count, int AmountHuf, IReadOnlyList<DateOnly> Dates);

/// <summary>A mai napot lefedő időszak. <paramref name="RemainingWorkingDays"/> a mai nappal együtt
/// számol, a kizárt napokat kihagyva.</summary>
public sealed record AdminPeriodDto(
    int Id,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    bool IsOpen,
    int RemainingWorkingDays);

public sealed record AdminMenuVariantDto(string Code, string Name, int Portions);

public sealed record AdminALaCarteOfferDto(string Name, int OrderedCount);

public sealed record AdminDailyPortionsDto(DateOnly Date, int Portions);

/// <summary>A halmozott diagram egy napja. A kategóriák hármas bontása szándékos: az
/// <c>ALaCarteCategory</c> öt értékéből a Leves és a Főétel egy sávba kerül (egy adag főétel levessel
/// egy fogyasztás), az Öntet pedig a Körethez, mert önmagában nem értelmezhető tétel.</summary>
public sealed record AdminALaCarteDayDto(DateOnly Date, int SoupAndMainCount, int SideDishCount, int DessertCount)
{
    public int Total => SoupAndMainCount + SideDishCount + DessertCount;
}

/// <summary>A „Területek" csempéinek élő számai — mindegyik egyetlen szám, ami eldönti, hogy érdemes-e
/// belépni az adott oldalra.</summary>
public sealed record AdminTileStatsDto(
    int OpenPeriodCount,
    int FutureExcludedDayCount,
    int ActiveOrderCountInPeriod,
    int ActiveALaCarteItemCount,
    int UsersWithBalanceCount,
    string? MyTodayVariantCode,
    int MyTodayALaCarteItemCount);

public sealed record AdminDashboardDto(
    DateOnly Today,
    AdminPeriodDto? ActivePeriod,
    IReadOnlyList<AdminTodoDto> Todos,
    int TodayMenuPortions,
    IReadOnlyList<AdminMenuVariantDto> TodayMenuVariants,
    int TodayCancelledPortions,
    int MissingDailyMenuCount,
    DateOnly? FirstMissingDailyMenu,
    bool TodayClosedByKitchen,
    int UnpaidInvoiceCount,
    int UnpaidInvoiceTotalHuf,
    IReadOnlyList<AdminDailyPortionsDto> WeeklyMenuPortions,
    IReadOnlyList<AdminALaCarteOfferDto> TodayALaCarteOffers,
    int TodayALaCarteItemCount,
    int TodayALaCarteUserCount,
    TimeOnly ALaCarteDeadline,
    bool ALaCarteDeadlinePassed,
    bool NextWorkingDayHasALaCarteOffer,
    IReadOnlyList<AdminALaCarteDayDto> WeeklyALaCarte,
    AdminTileStatsDto Tiles);
