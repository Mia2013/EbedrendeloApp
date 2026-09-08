using EbedrendeloApp.Common.Results;
using MediatR;

namespace EbedrendeloApp.Features.Menus.GetPeriodMenu;

/// <summary>
/// See <see cref="GetDailyMenu.GetDailyMenuQuery"/> for the IncludeUnpublished [A]/[U] rule.
///
/// A párjával ellentétben ez NEM <c>IRequireAdmin</c>, mert a dolgozói naptár is hívja — csak
/// <c>IncludeUnpublished: false</c>-szal. Itt tehát nem a kérés, hanem a <b>kapcsoló</b> az admin-jog,
/// és ezt a handler ellenőrzi (AC 2.5.2). Jelölő nem tudná kifejezni: a jelölők a kérés egészére
/// vonatkoznak, nem egy paraméterértékre.
/// </summary>
public sealed record GetPeriodMenuQuery(int OrderingPeriodId, bool IncludeUnpublished) : IRequest<Result<IReadOnlyList<DailyMenuDto>>>;
