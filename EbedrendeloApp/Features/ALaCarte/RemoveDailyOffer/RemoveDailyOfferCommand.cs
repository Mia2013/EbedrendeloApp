using EbedrendeloApp.Common.Results;
using MediatR;

namespace EbedrendeloApp.Features.ALaCarte.RemoveDailyOffer;

public sealed record RemoveDailyOfferCommand(int OfferId) : IRequest<Result>;
