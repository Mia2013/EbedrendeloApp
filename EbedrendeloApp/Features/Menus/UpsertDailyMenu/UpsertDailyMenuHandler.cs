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

        if (menu is null)
        {
            menu = new DailyMenu { Date = request.Date, IsPublished = false, Note = request.Note };
            db.DailyMenus.Add(menu);
        }
        else
        {
            menu.Note = request.Note;
            if (wasRevived)
            {
                menu.RemovedAtUtc = null;
                menu.IsPublished = false;
            }
        }

        var requestedCodes = request.Variants.Select(v => v.Code).ToHashSet(StringComparer.Ordinal);
        var toRemove = menu.Variants.Where(v => v.RemovedAtUtc is null && !requestedCodes.Contains(v.Code)).ToList();

        // Includes soft-deleted rows on purpose: a previously-removed code being requested again should
        // revive that row (the unique index on (DailyMenuId, Code) would otherwise conflict with a
        // fresh insert).
        var existingByCode = menu.Variants.ToDictionary(v => v.Code, StringComparer.Ordinal);

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

        if (!isFreshMenu)
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
}
