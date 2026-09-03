using EbedrendeloApp.Domain.Enums;
using MediatR;

namespace EbedrendeloApp.Features.ALaCarte.GetALaCarteDailySummary;

public sealed record GetALaCarteDailySummaryQuery(DateOnly Date) : IRequest<ALaCarteDailySummaryDto>;

public sealed record ALaCarteSummaryLineDto(ALaCarteCategory Category, string ItemName, int Count);

/// <summary>A <paramref name="SoupPortionCount"/> nem tárolt/külön lekérdezett érték, hanem a
/// Főétel-sorok darabszámából levezetve (AC 4.6.3) — minden Főétel-rendelés egy levesadagot is jelent,
/// hiszen a leves önállóan nem rendelhető (AC 4.2.8).</summary>
public sealed record ALaCarteDailySummaryDto(DateOnly Date, int SoupPortionCount, IReadOnlyList<ALaCarteSummaryLineDto> Lines);
