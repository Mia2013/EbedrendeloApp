using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Common.Results;
using MediatR;

namespace EbedrendeloApp.Features.Orders.CancelMenuOrders;

/// <summary>
/// Szándékosan <see cref="IActsOnBehalfOf"/> — nem <c>IAuditedOnBehalfOf</c>, mint a párja, a
/// <c>PlacePeriodOrderCommand</c>. A különbség nem következetlenség: rendelést leadni a kollégának
/// szívesség, lemondani viszont kárt okoz (elveszi az ebédjét, és a nap számlázottságától függően
/// pénzügyi hatása is van). Idegen <see cref="TargetUserId"/> ezért csak adminnak megy (AC 3.2.8).
///
/// A <see cref="TargetUserId"/> pozicionális property önmagában kielégíti az interfészt, nem kell
/// explicit implementáció.
/// </summary>
public sealed record CancelMenuOrdersCommand(
    int TargetUserId,
    int CancelledByUserId,
    IReadOnlyList<DateOnly> Dates) : IRequest<Result<BatchOrderResult>>, IActsOnBehalfOf;
