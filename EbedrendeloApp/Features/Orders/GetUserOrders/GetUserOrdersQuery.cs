using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Domain.Enums;
using MediatR;

namespace EbedrendeloApp.Features.Orders.GetUserOrders;

public sealed record GetUserOrdersQuery(int? OrderingPeriodId, int? UserId, OrderStatus? Status)
    : IRequest<Result<IReadOnlyList<UserOrderDto>>>;

public sealed record UserOrderDto(
    int OrderId,
    DateOnly Date,
    int UserId,
    string UserDisplayName,
    string VariantCode,
    string VariantName,
    OrderStatus Status,
    int PlacedByUserId,
    string PlacedByDisplayName,
    DateTime PlacedAtUtc,
    int? CancelledByUserId,
    string? CancelledByDisplayName,
    DateTime? CancelledAtUtc,
    CancellationReason? CancellationReason);
