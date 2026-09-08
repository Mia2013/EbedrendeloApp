using EbedrendeloApp.Common.Results;
using MediatR;

namespace EbedrendeloApp.Features.Kitchen.CloseDay;

/// <summary>US-6.2, AC 6.2.1 — "az összesítő elküldve" esemény: elmenti a nap záráskori
/// variánsonkénti darabszámát. Nincs dátumkorlát (múltbeli/mai/jövőbeli nap is zárható,
/// 01-szerver-architektura.md 11. fejezet 12. pont) — csak az elutasított, ha a nap már zárva van.</summary>
public sealed record CloseDayCommand(DateOnly Date, int ClosedByUserId) : IRequest<Result<KitchenClosureDto>>;
