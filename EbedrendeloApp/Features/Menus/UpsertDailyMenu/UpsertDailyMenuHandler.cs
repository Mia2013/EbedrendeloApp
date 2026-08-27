using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Services;
using EbedrendeloApp.Common.Time;
using EbedrendeloApp.Data;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.Menus.UpsertDailyMenu;

public sealed class UpsertDailyMenuHandler(
    IDbContextFactory<EbedrendeloDbContext> dbFactory,
    IAppClock clock,
    IMenuReassignmentService reassignmentService,
    INotificationService notificationService)
    : IRequestHandler<UpsertDailyMenuCommand, Result<int>>
{
    public async Task<Result<int>> Handle(UpsertDailyMenuCommand request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        if (await db.KitchenClosures.AnyAsync(k => k.Date == request.Date, cancellationToken))
        {
            return Result.Failure<int>(ErrorCodes.DayClosed, "A nap már le van zárva.");
        }

        var menu = await db.DailyMenus.Include(m => m.Variants).FirstOrDefaultAsync(m => m.Date == request.Date, cancellationToken);
        var nowUtc = clock.UtcNow.UtcDateTime;

        // A revived (previously soft-deleted) menu has no continuity with what came before it — all of
        // its orders were already cancelled by DeleteDailyMenuCommand — so it's treated like a create:
        // no MenuChanged spam below.
        var wasRevived = menu is { RemovedAtUtc: not null };
        var isFreshMenu = menu is null || wasRevived;

        // Snapshot the pre-change content so a no-op resave (admin reopens the day and saves without
        // touching anything) doesn't spam every active orderer with a MenuChanged notification — the
        // notification is meant for an actual name/description/variant-set change (01-szerver-architektura.md
        // §"Puszta módosításnál (név/leírás változik) ... MenuChanged értesítés"), not for "not a fresh menu".
        var previousNote = menu?.Note;
        var previousVariantSignature = isFreshMenu ? null : BuildVariantSignature(menu!.Variants.Where(v => v.RemovedAtUtc is null));

        if (menu is null)
        {
            // Nincs külön publikálás-lépés: a mentés (sikeres validáció = van legalább egy variáns)
            // azonnal rendelhetővé teszi a napot.
            menu = new DailyMenu { Date = request.Date, IsPublished = true, Note = request.Note };
            db.DailyMenus.Add(menu);
        }
        else
        {
            menu.Note = request.Note;
            if (wasRevived)
            {
                menu.RemovedAtUtc = null;
            }

            menu.IsPublished = true;
        }

        var requestedCodes = request.Variants.Select(v => v.Code).ToHashSet(StringComparer.Ordinal);
        var toRemove = menu.Variants.Where(v => v.RemovedAtUtc is null && !requestedCodes.Contains(v.Code)).ToList();

        // Includes soft-deleted rows on purpose: a previously-removed code being requested again should
        // revive that row (the unique index on (DailyMenuId, Code) would otherwise conflict with a
        // fresh insert).
        var existingByCode = menu.Variants.ToDictionary(v => v.Code, StringComparer.Ordinal);

        // Single batch load instead of a query per variant per dish-kind: the same (Kind, Name) lookup
        // MenuDishAllergenLookup uses for reads, but tracked here since UpdateDishAsync mutates in place.
        var dishes = await LoadTrackedDishesAsync(db, request.Variants, cancellationToken);

        foreach (var input in request.Variants)
        {
            if (existingByCode.TryGetValue(input.Code, out var variant))
            {
                variant.Name = input.Name;
                variant.Description = input.Description;
                variant.SortOrder = input.SortOrder;
                variant.RemovedAtUtc = null;
            }
            else
            {
                menu.Variants.Add(new MenuVariant
                {
                    DailyMenuId = menu.Id,
                    Code = input.Code,
                    Name = input.Name,
                    Description = input.Description,
                    SortOrder = input.SortOrder,
                });
            }

            UpdateDish(
                dishes, MenuDishKind.Leves, input.Name, input.SoupAllergens,
                input.SoupEnergyKcal, input.SoupFatGrams, input.SoupSaturatedFatGrams, input.SoupCarbohydrateGrams,
                input.SoupSugarGrams, input.SoupProteinGrams, input.SoupSaltGrams);
            if (!string.IsNullOrWhiteSpace(input.Description))
            {
                UpdateDish(
                    dishes, MenuDishKind.Foetel, input.Description, input.MainCourseAllergens,
                    input.MainCourseEnergyKcal, input.MainCourseFatGrams, input.MainCourseSaturatedFatGrams,
                    input.MainCourseCarbohydrateGrams, input.MainCourseSugarGrams, input.MainCourseProteinGrams,
                    input.MainCourseSaltGrams);
            }
        }

        // Flush so newly-added variants get real Ids before they can serve as a reassignment target below.
        await db.SaveChangesAsync(cancellationToken);

        var remainingVariants = menu.Variants.Where(v => v.RemovedAtUtc is null && !toRemove.Contains(v)).ToList();
        var touchedOrderIds = new HashSet<int>();

        foreach (var removed in toRemove)
        {
            var touched = await reassignmentService.ReassignOrCancelAsync(
                db, request.Date, removed, remainingVariants, request.PerformedByUserId, nowUtc, cancellationToken);
            foreach (var id in touched)
            {
                touchedOrderIds.Add(id);
            }

            removed.RemovedAtUtc = nowUtc;
        }

        var contentChanged = previousNote != request.Note
            || previousVariantSignature != BuildVariantSignature(remainingVariants);

        if (!isFreshMenu && contentChanged)
        {
            // AC 2.3.2: every active orderer of the day hears about the change, except the ones who
            // already got a more specific OrderReassigned/MenuCancelled notification above.
            var activeOrders = await db.MenuOrders
                .Where(o => o.Date == request.Date && o.Status == OrderStatus.Active)
                .ToListAsync(cancellationToken);

            foreach (var order in activeOrders)
            {
                if (touchedOrderIds.Contains(order.Id))
                {
                    continue;
                }

                notificationService.Notify(
                    db,
                    order.UserId,
                    NotificationType.MenuChanged,
                    "A napi menü módosult",
                    $"A(z) {request.Date:yyyy.MM.dd} napi menü adatai módosultak.",
                    nowUtc,
                    request.Date,
                    order.Id);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success(menu.Id);
    }

    /// <summary>
    /// Builds a comparable signature of a day's variant set (Code, Name, Description, SortOrder) so a
    /// before/after comparison can tell a real edit apart from a no-op resave. Deliberately excludes
    /// allergen/nutrition fields — those live on the shared MenuDish catalog, not on MenuVariant, and are
    /// out of scope for the "did this day's menu change" question the MenuChanged notification answers.
    /// </summary>
    private static string BuildVariantSignature(IEnumerable<MenuVariant> variants) => string.Join(
        '|',
        variants
            .OrderBy(v => v.Code, StringComparer.Ordinal)
            .Select(v => $"{v.Code}:{v.Name}:{v.Description}:{v.SortOrder}"));

    /// <summary>
    /// Batch-loads every dish catalog row this request could touch (tracked, not <see cref="MenuDishAllergenLookup"/>'s
    /// NoTracking read) in a single query, keyed the same way — avoids a per-variant per-dish-kind round trip in
    /// <see cref="UpdateDish"/> below.
    /// </summary>
    private static async Task<Dictionary<(MenuDishKind Kind, string Name), MenuDish>> LoadTrackedDishesAsync(
        EbedrendeloDbContext db, IReadOnlyList<MenuVariantInput> variants, CancellationToken cancellationToken)
    {
        var soupNames = variants.Select(v => v.Name).Distinct().ToList();
        var mainCourseNames = variants.Where(v => !string.IsNullOrWhiteSpace(v.Description))
            .Select(v => v.Description!).Distinct().ToList();

        var dishes = await db.MenuDishes
            .Where(d => (d.Kind == MenuDishKind.Leves && soupNames.Contains(d.Name))
                     || (d.Kind == MenuDishKind.Foetel && mainCourseNames.Contains(d.Name)))
            .ToListAsync(cancellationToken);

        return dishes.ToDictionary(d => (d.Kind, d.Name), MenuDishAllergenLookup.KeyComparer);
    }

    /// <summary>
    /// Updates an *existing* dish catalog row's allergens/nutrition in place when a daily menu referencing
    /// it is saved — a same-day correction doesn't need a separate screen. Does **not** create a new row
    /// for an unknown name: brand-new dishes are only ever added explicitly via
    /// Features/Menus/CreateMenuDish (the "+ Új étel" dialog), so a name that somehow doesn't match any
    /// catalog entry here is silently skipped rather than failing the whole save. A blank field (allergens
    /// or any nutrition value) is treated as "no change", so re-saving a day without touching that field
    /// can't accidentally wipe out data recorded earlier.
    /// </summary>
    private static void UpdateDish(
        IReadOnlyDictionary<(MenuDishKind Kind, string Name), MenuDish> dishes, MenuDishKind kind, string name, string? allergens,
        decimal? energyKcal, decimal? fatGrams, decimal? saturatedFatGrams, decimal? carbohydrateGrams,
        decimal? sugarGrams, decimal? proteinGrams, decimal? saltGrams)
    {
        if (!dishes.TryGetValue((kind, name), out var dish))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(allergens))
        {
            dish.Allergens = allergens.Trim();
        }

        dish.EnergyKcal = energyKcal ?? dish.EnergyKcal;
        dish.FatGrams = fatGrams ?? dish.FatGrams;
        dish.SaturatedFatGrams = saturatedFatGrams ?? dish.SaturatedFatGrams;
        dish.CarbohydrateGrams = carbohydrateGrams ?? dish.CarbohydrateGrams;
        dish.SugarGrams = sugarGrams ?? dish.SugarGrams;
        dish.ProteinGrams = proteinGrams ?? dish.ProteinGrams;
        dish.SaltGrams = saltGrams ?? dish.SaltGrams;
    }
}
