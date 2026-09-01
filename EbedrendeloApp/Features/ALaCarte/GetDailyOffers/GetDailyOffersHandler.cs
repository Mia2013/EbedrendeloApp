using EbedrendeloApp.Data;
using EbedrendeloApp.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.ALaCarte.GetDailyOffers;

public sealed class GetDailyOffersHandler(IDbContextFactory<EbedrendeloDbContext> dbFactory)
    : IRequestHandler<GetDailyOffersQuery, IReadOnlyList<ALaCarteDailyOfferDto>>
{
    public async Task<IReadOnlyList<ALaCarteDailyOfferDto>> Handle(GetDailyOffersQuery request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var offers = await db.ALaCarteDailyOffers
            .Include(o => o.ALaCarteItem)
            .Where(o => o.Date == request.Date)
            .ToListAsync(cancellationToken);

        return offers.Select(ToDto).OrderBy(d => d.Category).ThenBy(d => d.ItemName, StringComparer.Ordinal).ToList();
    }

    private static ALaCarteDailyOfferDto ToDto(ALaCarteDailyOffer o)
    {
        var item = o.ALaCarteItem!;
        return new ALaCarteDailyOfferDto(o.Id, o.Date, item.Id, item.Name, item.Category, item.PriceHuf, o.Capacity, o.OrderedCount, o.Capacity - o.OrderedCount);
    }
}
