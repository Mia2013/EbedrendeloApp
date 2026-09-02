using EbedrendeloApp.Features.ALaCarte.GetALaCarteDailySummary;
using MediatR;

namespace EbedrendeloApp.Features.ALaCarte.GetALaCarteMonthlySummary;

public sealed record GetALaCarteMonthlySummaryQuery(int Year, int Month) : IRequest<ALaCarteMonthlySummaryDto>;

/// <summary>A <paramref name="SoupPortionCount"/> ugyanúgy levezetett érték, mint a napi
/// <see cref="ALaCarteDailySummaryDto"/>-n (AC 4.6.3) — a hónap összes Főétel-sorának darabszáma.</summary>
public sealed record ALaCarteMonthlySummaryDto(int Year, int Month, int SoupPortionCount, IReadOnlyList<ALaCarteSummaryLineDto> Lines);
