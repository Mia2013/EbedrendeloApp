using EbedrendeloApp.Common.Results;
using MediatR;

namespace EbedrendeloApp.Features.Menus.GetPeriodMenu;

/// <summary>See <see cref="GetDailyMenu.GetDailyMenuQuery"/> for the IncludeUnpublished [A]/[U] rule.</summary>
public sealed record GetPeriodMenuQuery(int OrderingPeriodId, bool IncludeUnpublished) : IRequest<Result<IReadOnlyList<DailyMenuDto>>>;
