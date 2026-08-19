using MediatR;

namespace EbedrendeloApp.Features.Calendar.GetOrderableDays;

public sealed record GetOrderableDaysQuery(int OrderingPeriodId, int UserId) : IRequest<Common.Results.Result<IReadOnlyList<OrderableDayDto>>>;

public sealed record OrderableDayDto(
    DateOnly Date,
    bool Orderable,
    bool Cancellable,
    string? VariantCode,
    string? VariantName,
    string? Reason,
    string? ReasonDetail);
