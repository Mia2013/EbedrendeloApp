using EbedrendeloApp.Common.Results;
using MediatR;

namespace EbedrendeloApp.Features.ALaCarte.DeactivateALaCarteItem;

public sealed record DeactivateALaCarteItemCommand(int Id) : IRequest<Result>;
