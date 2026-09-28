using EbedrendeloApp.Common.Security;
using MediatR;

namespace EbedrendeloApp.Features.Admin.GetAdminDashboard;

/// <summary>
/// Az admin áttekintő oldal (<c>/admin</c>) egyetlen lekérdezése. Szándékosan egy hívás és nem
/// nyolc: az oldal minden doboza ugyanabból az egyetlen óra-leolvasásból számolt mai napra és
/// ugyanarra az aktív időszakra vonatkozik — külön hívásokkal az éjféli napforduló két kártyát két
/// különböző napra vihetne. Adatbázis-szintű pillanatkép (tranzakció) <b>nincs</b>: a részlekérdezések
/// között befutó rendelés egy-egy számot eltolhat, ez egy áttekintőnél elfogadható.
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

    /// <summary>A mai napot még nem zárta le a konyha, pedig van menüadag, amit összesíteni kell.
    /// Az à la carte nem része a napzárásnak (AC 6.1.1), ezért itt sem számít.</summary>
    KitchenDayOpen,

    /// <summary>Van kiállított, de kifizetetlen számla.</summary>
    UnpaidInvoices,

    /// <summary>A következő, időszakkal lefedett munkanapra nincs önállóan rendelhető (nem leves,
    /// nem nullázott) à la carte tétel kiajánlva.</summary>
    MissingALaCarteOffer,
}

/// <summary><paramref name="Dates"/> a <see cref="AdminTodoKind.MissingDailyMenu"/> ágon a hiányzó
/// napok, a <see cref="AdminTodoKind.MissingALaCarteOffer"/> ágon az az egy nap, amire kínálat kell;
/// <paramref name="AmountHuf"/> csak a számlás ágon van kitöltve.</summary>
public sealed record AdminTodoDto(AdminTodoKind Kind, int Count, int AmountHuf, IReadOnlyList<DateOnly> Dates);

/// <summary>A mai napot lefedő időszak. <paramref name="RemainingWorkingDays"/> a mai nappal együtt
/// számol, a kizárt napokat kihagyva.</summary>
public sealed record AdminPeriodDto(
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    bool IsOpen,
    int RemainingWorkingDays);

public sealed record AdminMenuVariantDto(string Code, string Name, int Portions);

public sealed record AdminALaCarteOfferDto(string Name, int OrderedCount);

/// <summary>A halmozott diagram egy napja. A kategóriák hármas bontása szándékos: az
/// <c>ALaCarteCategory</c> öt értékéből a Leves és a Főétel egy sávba kerül (egy adag főétel levessel
/// egy fogyasztás), az Öntet pedig a Körethez, mert önmagában nem értelmezhető tétel.</summary>
public sealed record AdminALaCarteDayDto(DateOnly Date, int SoupAndMainCount, int SideDishCount, int DessertCount);

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

/// <summary><paramref name="TodayIsServiceDay"/>: munkanap és nincs kizárva — hétvégén és kizárt
/// napon a „mára nincs menü / nincs kínálat / nyitott konyha" nem hiba, a felület nem figyelmeztet.
/// <paramref name="TodayCancelledPortions"/>: a mai napra lemondott és <b>nem pótolt</b> adagok — a
/// variáns-csere (lemondás + új rendelés) nem számít bele.
/// <paramref name="NextALaCarteDay"/>: a következő, időszakkal lefedett munkanap; <c>null</c>, ha
/// belátható időn belül nincs ilyen (akkor nincs mire kínálatot feltenni).</summary>
public sealed record AdminDashboardDto(
    DateOnly Today,
    bool TodayIsServiceDay,
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
    IReadOnlyList<AdminALaCarteOfferDto> TodayALaCarteOffers,
    int TodayALaCarteItemCount,
    int TodayALaCarteUserCount,
    TimeOnly ALaCarteDeadline,
    bool ALaCarteDeadlinePassed,
    DateOnly? NextALaCarteDay,
    bool NextALaCarteDayHasOffer,
    IReadOnlyList<AdminALaCarteDayDto> WeeklyALaCarte,
    AdminTileStatsDto Tiles);
