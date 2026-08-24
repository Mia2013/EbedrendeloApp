using MediatR;

namespace EbedrendeloApp.Features.Menus.GetDailyMenu;

/// <summary>
/// <paramref name="IncludeUnpublished"/> should be true only for admin [A] callers (AC 2.5.2) — a
/// worker-facing [U] caller must pass false so a not-yet-published day comes back as "no menu".
/// </summary>
public sealed record GetDailyMenuQuery(DateOnly Date, bool IncludeUnpublished) : IRequest<DailyMenuDto?>;
