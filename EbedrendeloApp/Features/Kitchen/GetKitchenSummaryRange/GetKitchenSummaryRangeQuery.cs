using MediatR;

namespace EbedrendeloApp.Features.Kitchen.GetKitchenSummaryRange;

/// <summary>US-6.1, AC 6.1.2 — dátumtartomány (nem <c>OrderingPeriodId</c>-hez kötve, ld. a
/// visszamenőleges riport-jelleget) élő adagszáma variánsonként, naponkénti bontásban. Csak azok a
/// napok szerepelnek az eredményben, amelyekre van legalább egy aktív rendelés.</summary>
public sealed record GetKitchenSummaryRangeQuery(DateOnly From, DateOnly To) : IRequest<IReadOnlyList<KitchenSummaryDto>>;
