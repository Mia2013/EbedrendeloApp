using MediatR;

namespace EbedrendeloApp.Features.Calendar.GetUncoveredWorkdays;

public sealed record GetUncoveredWorkdaysQuery(DateOnly From, DateOnly To) : IRequest<IReadOnlyList<DateOnly>>;
