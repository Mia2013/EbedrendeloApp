using EbedrendeloApp.Common.Results;
using MediatR;

namespace EbedrendeloApp.Features.Calendar.UpsertOrderingPeriod;

public sealed record UpsertOrderingPeriodCommand(
    int? Id,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    DateTime OrderDeadline,
    bool IsOpen) : IRequest<Result<OrderingPeriodDto>>;
