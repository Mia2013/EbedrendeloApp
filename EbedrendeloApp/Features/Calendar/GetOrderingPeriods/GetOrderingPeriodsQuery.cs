using MediatR;

namespace EbedrendeloApp.Features.Calendar.GetOrderingPeriods;

public sealed record GetOrderingPeriodsQuery : IRequest<IReadOnlyList<OrderingPeriodDto>>;
