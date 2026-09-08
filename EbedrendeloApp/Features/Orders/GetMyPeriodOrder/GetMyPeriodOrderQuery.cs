using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Domain.Enums;
using MediatR;

namespace EbedrendeloApp.Features.Orders.GetMyPeriodOrder;

/// <summary>Egy időszak <b>teljes</b> rendeléstörténete egy felhasználóra — múltbeli napokkal, lemondott
/// sorokkal együtt. Ez pontosan az, amit a más nevében rendelő dolgozónak nem szabad látnia (AC 3.1.9),
/// ezért <see cref="IActsOnBehalfOf"/> és nem <c>IAuditedOnBehalfOf</c>: idegen <c>UserId</c> csak
/// adminnak. A kolléga nevében rendeléshez ez a lekérdezés nem kell — arra a <c>GetOrderableDaysQuery</c>
/// való, ami idegen nézetben eleve csak a mai naptól előre ad sorokat.</summary>
public sealed record GetMyPeriodOrderQuery(int UserId, int OrderingPeriodId)
    : IRequest<Result<IReadOnlyList<MyPeriodOrderDto>>>, IActsOnBehalfOf
{
    int IActsOnBehalfOf.TargetUserId => UserId;
}

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
