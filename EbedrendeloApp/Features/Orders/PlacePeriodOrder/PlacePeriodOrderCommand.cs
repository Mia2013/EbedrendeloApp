using EbedrendeloApp.Common.Results;
using MediatR;

namespace EbedrendeloApp.Features.Orders.PlacePeriodOrder;

public sealed record PlacePeriodOrderCommand(
    int TargetUserId,
    int PlacedByUserId,
    int OrderingPeriodId,
    IReadOnlyList<DayOrderRequest> Days) : IRequest<Result<BatchOrderResult>>;

public sealed record DayOrderRequest(DateOnly Date, string VariantCode);
