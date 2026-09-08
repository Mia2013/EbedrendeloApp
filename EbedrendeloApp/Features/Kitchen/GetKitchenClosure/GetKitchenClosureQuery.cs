using MediatR;

namespace EbedrendeloApp.Features.Kitchen.GetKitchenClosure;

/// <summary>US-6.3 — a nap legutóbbi zárási pillanatképe. <c>null</c>, ha erre a napra sosem volt zárás.</summary>
public sealed record GetKitchenClosureQuery(DateOnly Date) : IRequest<KitchenClosureDto?>;
