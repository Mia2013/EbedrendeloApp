using EbedrendeloApp.Data;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Common.Services;

public sealed class MenuReassignmentService(ICreditService creditService, INotificationService notificationService)
    : IMenuReassignmentService
{
    public async Task<IReadOnlyList<int>> ReassignOrCancelAsync(
        EbedrendeloDbContext db,
        DateOnly date,
        MenuVariant removedVariant,
        IReadOnlyList<MenuVariant> remainingVariants,
        int performedByUserId,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var affectedOrders = await db.MenuOrders
            .Where(o => o.Date == date && o.MenuVariantId == removedVariant.Id && o.Status == OrderStatus.Active)
            .ToListAsync(cancellationToken);

        if (affectedOrders.Count == 0)
        {
            return [];
        }

        // "A menü legyen a default" — smallest SortOrder, then Code, wins the reassignment target.
        var target = remainingVariants.OrderBy(v => v.SortOrder).ThenBy(v => v.Code, StringComparer.Ordinal).FirstOrDefault();

        foreach (var order in affectedOrders)
        {
            if (target is null)
            {
                order.Status = OrderStatus.Cancelled;
                order.CancelledAtUtc = nowUtc;
                order.CancelledByUserId = performedByUserId;
                order.CancellationReason = CancellationReason.VariantRemoved;

                creditService.IssueCancellationCredit(db, order, performedByUserId, nowUtc);
                notificationService.Notify(
                    db,
                    order.UserId,
                    NotificationType.MenuCancelled,
                    "Rendelésed lemondásra került",
                    $"A(z) {date:yyyy.MM.dd} napi {removedVariant.Code} menü megszűnt, más variáns nem maradt a napon, a rendelésed jóváírásra került.",
                    nowUtc,
                    date,
                    order.Id);
            }
            else
            {
                var oldCode = removedVariant.Code;
                order.ReassignedFromVariantCode = oldCode;
                order.MenuVariantId = target.Id;
                order.ReassignedAtUtc = nowUtc;

                notificationService.Notify(
                    db,
                    order.UserId,
                    NotificationType.OrderReassigned,
                    "Rendelésed átvezetésre került",
                    $"A(z) {date:yyyy.MM.dd} napi {oldCode} menü megszűnt, a rendelésed átkerült a(z) {target.Code} menüre.",
                    nowUtc,
                    date,
                    order.Id);

                if (order.PlacedByUserId != order.UserId)
                {
                    notificationService.Notify(
                        db,
                        order.PlacedByUserId,
                        NotificationType.OrderReassigned,
                        "Az általad leadott rendelés átvezetésre került",
                        $"A(z) {date:yyyy.MM.dd} napi {oldCode} menü megszűnt, az általad leadott rendelés átkerült a(z) {target.Code} menüre.",
                        nowUtc,
                        date,
                        order.Id);
                }
            }
        }

        return affectedOrders.Select(o => o.Id).ToList();
    }
}
