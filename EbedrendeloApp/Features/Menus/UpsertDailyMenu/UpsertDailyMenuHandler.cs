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
        if (request.Date < clock.Today)
        {
            return Result.Failure<int>(ErrorCodes.NotFutureDate, "Elmúlt nap menüje már nem módosítható.");
        }

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

        // Single batch load instead of a query per variant per dish: tracked, since UpdateDish mutates in
        // place. Keyed by Id — the admin already picked these from the catalog via MudAutocomplete, so
        // there's no name to match, only a straight lookup.
        var dishes = await LoadTrackedDishesAsync(db, request.Variants, cancellationToken);

        foreach (var input in request.Variants)
        {
            if (!dishes.TryGetValue(input.SoupDishId, out var soupDish))
            {
                return Result.Failure<int>(ErrorCodes.NotFound, $"A(z) {input.Code} variáns levese nem található a katalógusban.");
            }

            MenuDish? mainCourseDish = null;
            if (input.MainCourseDishId is { } mainCourseDishId && !dishes.TryGetValue(mainCourseDishId, out mainCourseDish))
            {
                return Result.Failure<int>(ErrorCodes.NotFound, $"A(z) {input.Code} variáns főétele nem található a katalógusban.");
            }

            if (existingByCode.TryGetValue(input.Code, out var variant))
            {
                variant.SoupDishId = input.SoupDishId;
                variant.SoupName = soupDish.Name;
                variant.MainCourseDishId = input.MainCourseDishId;
                variant.MainCourseName = mainCourseDish?.Name;
                variant.SortOrder = input.SortOrder;
                variant.RemovedAtUtc = null;
            }
            else
            {
                menu.Variants.Add(new MenuVariant
                {
                    DailyMenuId = menu.Id,
                    Code = input.Code,
                    SoupDishId = input.SoupDishId,
                    SoupName = soupDish.Name,
                    MainCourseDishId = input.MainCourseDishId,
                    MainCourseName = mainCourseDish?.Name,
                    SortOrder = input.SortOrder,
                });
            }

            UpdateDish(
                soupDish, input.SoupAllergens,
                input.SoupEnergyKcal, input.SoupFatGrams, input.SoupSaturatedFatGrams, input.SoupCarbohydrateGrams,
                input.SoupSugarGrams, input.SoupProteinGrams, input.SoupSaltGrams);
            if (mainCourseDish is not null)
            {
                UpdateDish(
                    mainCourseDish, input.MainCourseAllergens,
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
            .Select(v => $"{v.Code}:{v.SoupName}:{v.MainCourseName}:{v.SortOrder}"));

    /// <summary>
    /// Batch-loads every dish catalog row this request could touch (tracked, not <see cref="MenuDishAllergenLookup"/>'s
    /// NoTracking read) in a single query, keyed by Id — avoids a per-variant round trip in
    /// <see cref="UpdateDish"/> below.
    /// </summary>
    private static async Task<Dictionary<int, MenuDish>> LoadTrackedDishesAsync(
        EbedrendeloDbContext db, IReadOnlyList<MenuVariantInput> variants, CancellationToken cancellationToken)
    {
        var dishIds = variants
            .Select(v => (int?)v.SoupDishId)
            .Concat(variants.Select(v => v.MainCourseDishId))
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var dishes = await db.MenuDishes.Where(d => dishIds.Contains(d.Id)).ToListAsync(cancellationToken);
        return dishes.ToDictionary(d => d.Id);
    }

    /// <summary>
    /// Updates an existing dish catalog row's allergens/nutrition in place when a daily menu referencing
    /// it is saved — a same-day correction doesn't need a separate screen. A blank field (allergens or any
    /// nutrition value) is treated as "no change", so re-saving a day without touching that field can't
    /// accidentally wipe out data recorded earlier.
    /// </summary>
    private static void UpdateDish(
        MenuDish dish, string? allergens,
        decimal? energyKcal, decimal? fatGrams, decimal? saturatedFatGrams, decimal? carbohydrateGrams,
        decimal? sugarGrams, decimal? proteinGrams, decimal? saltGrams)
    {
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
