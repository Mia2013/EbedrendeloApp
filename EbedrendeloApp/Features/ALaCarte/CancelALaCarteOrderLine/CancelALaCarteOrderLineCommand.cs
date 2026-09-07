using EbedrendeloApp.Common.Results;
using MediatR;

namespace EbedrendeloApp.Features.ALaCarte.CancelALaCarteOrderLine;

public sealed record CancelALaCarteOrderLineCommand(int UserId, int ALaCarteItemId) : IRequest<Result>;
