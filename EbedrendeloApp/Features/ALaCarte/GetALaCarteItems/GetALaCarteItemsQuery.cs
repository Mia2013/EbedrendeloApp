using MediatR;

namespace EbedrendeloApp.Features.ALaCarte.GetALaCarteItems;

public sealed record GetALaCarteItemsQuery : IRequest<IReadOnlyList<ALaCarteItemDto>>;
