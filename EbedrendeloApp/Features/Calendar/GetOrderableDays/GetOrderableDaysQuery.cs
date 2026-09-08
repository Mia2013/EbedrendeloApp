using EbedrendeloApp.Common.Security;
using MediatR;

namespace EbedrendeloApp.Features.Calendar.GetOrderableDays;

public sealed record GetOrderableDaysQuery(int OrderingPeriodId, int UserId) : IRequest<Common.Results.Result<IReadOnlyList<OrderableDayDto>>>, IActsOnBehalfOf
{
    int IActsOnBehalfOf.TargetUserId => UserId;
}

public sealed record OrderableDayDto(
    DateOnly Date,
    bool Orderable,
    bool Cancellable,
    string? VariantCode,
    string? VariantName,
    string? Reason,
    string? ReasonDetail,
    int MenuPortionHuf = 0);
