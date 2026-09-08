using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Common.Results;
using MediatR;

namespace EbedrendeloApp.Features.Orders.CancelMenuOrders;

public sealed record CancelMenuOrdersCommand(
    int TargetUserId,
    int CancelledByUserId,
    IReadOnlyList<DateOnly> Dates) : IRequest<Result<BatchOrderResult>>, IAuditedOnBehalfOf;
