using EbedrendeloApp.Common.Services;
using EbedrendeloApp.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.Kitchen.GetKitchenSummary;

public sealed class GetKitchenSummaryHandler(IDbContextFactory<EbedrendeloDbContext> dbFactory)
    : IRequestHandler<GetKitchenSummaryQuery, KitchenSummaryDto>
{
    public async Task<KitchenSummaryDto> Handle(GetKitchenSummaryQuery request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var live = await KitchenSummaryLines.LoadLiveVariantsAsync(db, request.Date, request.Date, cancellationToken);
        var ordered = await KitchenSummaryLines.LoadOrderedVariantsAsync(db, request.Date, request.Date, cancellationToken);

        var lines = KitchenSummaryLines.Build(live, ordered);

        var isClosed = await KitchenClosureQueries.IsClosedAsync(db, request.Date, cancellationToken);

        return new KitchenSummaryDto(request.Date, isClosed, lines, lines.Sum(l => l.Quantity));
    }
}
