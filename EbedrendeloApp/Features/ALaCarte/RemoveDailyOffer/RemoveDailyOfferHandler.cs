using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.ALaCarte.RemoveDailyOffer;

public sealed class RemoveDailyOfferHandler(IDbContextFactory<EbedrendeloDbContext> dbFactory)
    : IRequestHandler<RemoveDailyOfferCommand, Result>
{
    public async Task<Result> Handle(RemoveDailyOfferCommand request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var offer = await db.ALaCarteDailyOffers.FirstOrDefaultAsync(o => o.Id == request.OfferId, cancellationToken);
        if (offer is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "Az ajánlat nem található.");
        }

        // AC 4.4.1/4.4.2 — csak érintetlen ajánlat vonható vissza (Leves-ajánlatra OrderedCount sosem
        // nő, tehát az mindig szabadon visszavonható).
        if (offer.OrderedCount != 0)
        {
            return Result.Failure(ErrorCodes.HasOrders, "Az ajánlat nem vonható vissza, mert már van rá rendelés.");
        }

        db.ALaCarteDailyOffers.Remove(offer);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
