using MediatR;

namespace EbedrendeloApp.Features.Kitchen.GetKitchenSummary;

/// <summary>US-6.1, AC 6.1.1 — egy nap élő adagszáma variánsonként.</summary>
public sealed record GetKitchenSummaryQuery(DateOnly Date) : IRequest<KitchenSummaryDto>;
