using MediatR;

namespace EbedrendeloApp.Features.Calendar.GetOrderingPeriod;

public sealed record GetOrderingPeriodQuery(int Id) : IRequest<OrderingPeriodDto?>;
