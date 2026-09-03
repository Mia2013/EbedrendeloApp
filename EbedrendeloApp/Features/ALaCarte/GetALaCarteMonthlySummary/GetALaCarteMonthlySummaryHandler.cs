using EbedrendeloApp.Data;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.ALaCarte.GetALaCarteDailySummary;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.ALaCarte.GetALaCarteMonthlySummary;

public sealed class GetALaCarteMonthlySummaryHandler(IDbContextFactory<EbedrendeloDbContext> dbFactory)
    : IRequestHandler<GetALaCarteMonthlySummaryQuery, ALaCarteMonthlySummaryDto>
{
    public async Task<ALaCarteMonthlySummaryDto> Handle(GetALaCarteMonthlySummaryQuery request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var startDate = new DateOnly(request.Year, request.Month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        var lines = await db.ALaCarteOrderLines
            .Where(l => l.ALaCarteOrder!.Date >= startDate && l.ALaCarteOrder!.Date <= endDate)
            .Select(l => new { l.ALaCarteOrder!.Date, l.CategorySnapshot, l.ItemNameSnapshot, l.IncludesSoup })
            .ToListAsync(cancellationToken);

        var grouped = lines
            .GroupBy(l => (l.Date, l.CategorySnapshot, l.ItemNameSnapshot))
            .Select(g => new ALaCarteMonthlyLineDto(g.Key.Date, g.Key.CategorySnapshot, g.Key.ItemNameSnapshot, g.Count()))
            .ToList();

        var offers = await db.ALaCarteDailyOffers
            .Where(o => o.Date >= startDate && o.Date <= endDate && o.Capacity > 0)
            .Select(o => new { o.Date, o.ALaCarteItem!.Category, o.ALaCarteItem!.Name })
            .ToListAsync(cancellationToken);

        // A leves sosem önálló rendelési sor (AC 4.2.8) — a napi kínálatból (melyik levest ajánlották
        // aznap) és a IncludesSoup=true Főétel-sorok napi darabszámából állítjuk elő szintetikus sorként,
        // hogy a havi mátrixban a többi kategóriával egyenrangú oszlop-csoportként jelenhessen meg.
        // ToDictionary itt elszállna, ha egy napra adatanomália miatt (lásd 01-szerver-architektura.md
        // 13. ismert korlátozás — az "legfeljebb egy leves naponta" szabály csak handler-szinten, DB
        // constraint nélkül él) mégis két aktív leves-ajánlat kerülne — ezért determinisztikusan
        // (ábécé szerint első) választunk egyet, ahelyett hogy a lekérdezés összeomlana.
        var soupNameByDate = offers
            .Where(o => o.Category == ALaCarteCategory.Leves)
            .GroupBy(o => o.Date)
            .ToDictionary(g => g.Key, g => g.OrderBy(o => o.Name, StringComparer.Ordinal).First().Name);
        var soupCountsByDate = lines.Where(l => l.IncludesSoup).GroupBy(l => l.Date).ToDictionary(g => g.Key, g => g.Count());
        foreach (var (date, count) in soupCountsByDate)
        {
            var soupName = soupNameByDate.GetValueOrDefault(date, "Leves");
            grouped.Add(new ALaCarteMonthlyLineDto(date, ALaCarteCategory.Leves, soupName, count));
        }

        grouped = grouped.OrderBy(l => l.Date).ThenBy(l => l.Category).ThenBy(l => l.ItemName, StringComparer.Ordinal).ToList();

        // Egy tétel csak akkor kap oszlopot a havi mátrixban, ha a hónap legalább egy napján ténylegesen
        // rendelhető volt (Capacity > 0) — függetlenül attól, hogy lett-e ténylegesen rendelés belőle.
        // Ha a hónapban egyszer sem volt így kínálva, nincs oszlopa (nem torzítja a listát egy azóta
        // kivezetett vagy még fel sem vett tétel).
        var offeredItems = offers
            .Select(o => new ALaCarteMonthlyOfferedItemDto(o.Category, o.Name))
            .Distinct()
            .ToList();

        var soupPortionCount = lines.Count(l => l.CategorySnapshot == ALaCarteCategory.Foetel);

        return new ALaCarteMonthlySummaryDto(request.Year, request.Month, soupPortionCount, grouped, offeredItems);
    }
}
