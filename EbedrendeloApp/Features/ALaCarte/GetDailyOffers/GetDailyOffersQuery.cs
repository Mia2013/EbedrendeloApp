using MediatR;

namespace EbedrendeloApp.Features.ALaCarte.GetDailyOffers;

/// <summary>A nyers admin-nézet — a Leves ajánlatot NEM szűri ki (ellentétben
/// GetTodayMenuForUserHandler-rel, ami a dolgozói nézethez AC 4.5.1 szerint kizárja).</summary>
public sealed record GetDailyOffersQuery(DateOnly Date) : IRequest<IReadOnlyList<ALaCarteDailyOfferDto>>;
