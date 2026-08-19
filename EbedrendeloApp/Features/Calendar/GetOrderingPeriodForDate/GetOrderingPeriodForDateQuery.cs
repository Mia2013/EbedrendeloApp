using MediatR;

namespace EbedrendeloApp.Features.Calendar.GetOrderingPeriodForDate;

public sealed record GetOrderingPeriodForDateQuery(DateOnly Date) : IRequest<OrderingPeriodDto?>;
