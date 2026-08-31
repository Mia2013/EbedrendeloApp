using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Domain.Enums;
using MediatR;

namespace EbedrendeloApp.Features.Orders.GetMyPeriodOrder;

public sealed record GetMyPeriodOrderQuery(int UserId, int OrderingPeriodId)
    : IRequest<Result<IReadOnlyList<MyPeriodOrderDto>>>;

public sealed record MyPeriodOrderDto(
    DateOnly Date,
    OrderStatus Status,
    string VariantCode,
    string VariantName,
    int PlacedByUserId,
    string? PlacedByDisplayName,
    DateTime PlacedAtUtc,
    CancellationReason? CancellationReason,
    DateTime? CancelledAtUtc,
    string? ReassignedFromVariantCode);
