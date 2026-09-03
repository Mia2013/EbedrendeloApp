using EbedrendeloApp.Common.Results;
using MediatR;

namespace EbedrendeloApp.Features.ALaCarte.PlaceALaCarteOrder;

/// <summary>Nincs "más nevében rendelés" — <c>PlacedByUserId</c> mindig <paramref name="UserId"/>.
/// Nincs részleges siker: a lista minden tagja egyszerre reserválódik, vagy a teljes hívás elbukik.</summary>
public sealed record PlaceALaCarteOrderCommand(int UserId, IReadOnlyList<int> ALaCarteItemIds) : IRequest<Result<PlacedALaCarteOrderLinesDto>>;
