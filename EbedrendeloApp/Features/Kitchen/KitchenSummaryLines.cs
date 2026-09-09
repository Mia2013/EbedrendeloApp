using EbedrendeloApp.Data;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Orders;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.Kitchen;

/// <summary>Egy nap élő menüvariánsa — a nullás összesítő-sorok forrása.</summary>
public sealed record KitchenMenuVariantRow(DateOnly Date, int VariantId, string Code, string SoupName, string? MainCourseName);

/// <summary>Egy variánsra leadott aktív rendelések darabszáma egy napon.</summary>
public sealed record KitchenOrderedVariantRow(DateOnly Date, int VariantId, string Code, string SoupName, string? MainCourseName, int Quantity);

/// <summary>
/// A konyhai összesítő sorainak közös építése (AC 6.1.1: „variánsonként (A/B/C)"). Három use case
/// osztozik rajta — <c>GetKitchenSummary</c>, <c>GetKitchenSummaryRange</c> és <c>CloseDay</c> —, hogy
/// az élő képernyő, az időszaki lista és a záráskori pillanatkép ugyanazt mutassa.
///
/// A lényeg, amiért ez nem egyszerű „csoportosítás rendelésenként": a nap **minden** publikált
/// variánsa megjelenik, a nem rendelt is, 0 adaggal. Enélkül a konyha nem tudja megkülönböztetni a
/// „B menüre nem rendelt senki" esetet attól, hogy a B menü kimaradt a listából.
/// </summary>
public static class KitchenSummaryLines
{
    /// <summary>A tartomány publikált, nem törölt napi menüinek nem törölt variánsai.</summary>
    public static async Task<IReadOnlyList<KitchenMenuVariantRow>> LoadLiveVariantsAsync(
        EbedrendeloDbContext db, DateOnly from, DateOnly to, CancellationToken cancellationToken)
        // Szándékosan a variánsok felől, nem a napi menü Variants navigációján át: a beágyazott
        // SelectMany korrelált alkérdést (SQL APPLY) fordítana, amit a handler-tesztek Sqlite-ja nem tud.
        => await db.MenuVariants
            .Where(v => v.RemovedAtUtc == null
                && v.DailyMenu!.Date >= from && v.DailyMenu.Date <= to
                && v.DailyMenu.IsPublished && v.DailyMenu.RemovedAtUtc == null)
            .Select(v => new KitchenMenuVariantRow(v.DailyMenu!.Date, v.Id, v.Code, v.SoupName, v.MainCourseName))
            .ToListAsync(cancellationToken);

    /// <summary>AC 6.1.3: kizárólag az <see cref="OrderStatus.Active"/> rendelés számít; a lemondott
    /// bele se kerül ebbe az összekapcsolásba.</summary>
    public static async Task<IReadOnlyList<KitchenOrderedVariantRow>> LoadOrderedVariantsAsync(
        EbedrendeloDbContext db, DateOnly from, DateOnly to, CancellationToken cancellationToken)
        => await db.MenuOrders
            .Where(o => o.Date >= from && o.Date <= to && o.Status == OrderStatus.Active)
            .Join(db.MenuVariants, o => o.MenuVariantId, v => v.Id, (o, v) => new { o.Date, Variant = v })
            .GroupBy(x => new { x.Date, x.Variant.Id, x.Variant.Code, x.Variant.SoupName, x.Variant.MainCourseName })
            .Select(g => new KitchenOrderedVariantRow(
                g.Key.Date, g.Key.Id, g.Key.Code, g.Key.SoupName, g.Key.MainCourseName, g.Count()))
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Egy nap sorai: minden élő variáns (a nem rendelt is, 0-val), plusz azok a rendelések, amelyek
    /// variánsa már nem szerepel a nap élő menüjében. Ez utóbbi nem elméleti eset: ha az admin a
    /// rendelések leadása után törli vagy publikálatlanná teszi a variánst, az adagot akkor is meg kell
    /// főzni — ezért nem eshet ki a felsorolásból.
    /// </summary>
    public static IReadOnlyList<KitchenVariantLineDto> Build(
        IEnumerable<KitchenMenuVariantRow> liveVariants,
        IEnumerable<KitchenOrderedVariantRow> orderedVariants)
    {
        var live = liveVariants.ToList();
        var ordered = orderedVariants.ToList();
        var quantityByVariantId = ordered.ToDictionary(o => o.VariantId, o => o.Quantity);
        var liveVariantIds = live.Select(v => v.VariantId).ToHashSet();

        return live
            .Select(v => new KitchenVariantLineDto(
                v.Code,
                VariantDisplayName.Combine(v.SoupName, v.MainCourseName),
                quantityByVariantId.GetValueOrDefault(v.VariantId)))
            .Concat(ordered
                .Where(o => !liveVariantIds.Contains(o.VariantId))
                .Select(o => new KitchenVariantLineDto(
                    o.Code,
                    VariantDisplayName.Combine(o.SoupName, o.MainCourseName),
                    o.Quantity)))
            .OrderBy(l => l.VariantCode, StringComparer.Ordinal)
            .ToList();
    }
}
