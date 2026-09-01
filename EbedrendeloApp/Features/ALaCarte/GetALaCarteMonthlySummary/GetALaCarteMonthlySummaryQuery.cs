using EbedrendeloApp.Features.ALaCarte.GetALaCarteDailySummary;
using MediatR;

namespace EbedrendeloApp.Features.ALaCarte.GetALaCarteMonthlySummary;

public sealed record GetALaCarteMonthlySummaryQuery(int Year, int Month) : IRequest<ALaCarteMonthlySummaryDto>;

public sealed record ALaCarteMonthlySummaryDto(int Year, int Month, IReadOnlyList<ALaCarteSummaryLineDto> Lines);
