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

                var credit = creditService.IssueCancellationCredit(db, order, performedByUserId, nowUtc);
                notificationService.NotifyOrderCancelled(
                    db,
                    order,
                    credit,
                    new OrderNotificationText(
                        "Rendelésed lemondásra került",
                        credit is null
                            ? $"A(z) {date:yyyy.MM.dd} napi {removedVariant.Code} menü megszűnt, más variáns nem maradt a napon, a rendelésed lemondásra került. Ez a nap még nem volt kiszámlázva, így nem kerül rá számlára."
                            : $"A(z) {date:yyyy.MM.dd} napi {removedVariant.Code} menü megszűnt, más variáns nem maradt a napon, a rendelésed jóváírásra került."),
                    new OrderNotificationText(
                        "Az általad leadott rendelés lemondásra került",
                        $"A(z) {date:yyyy.MM.dd} napi {removedVariant.Code} menü megszűnt, más variáns nem maradt a napon, az általad leadott rendelés lemondásra került."),
                    performedByUserId,
                    nowUtc);
            }
            else
            {
                var oldCode = removedVariant.Code;
                order.ReassignedFromVariantCode = oldCode;
                order.MenuVariantId = target.Id;
                order.ReassignedAtUtc = nowUtc;

                notificationService.NotifyOrderParties(
                    db,
                    order,
                    NotificationType.OrderReassigned,
                    new OrderNotificationText(
                        "Rendelésed átvezetésre került",
                        $"A(z) {date:yyyy.MM.dd} napi {oldCode} menü megszűnt, a rendelésed átkerült a(z) {target.Code} menüre."),
                    new OrderNotificationText(
                        "Az általad leadott rendelés átvezetésre került",
                        $"A(z) {date:yyyy.MM.dd} napi {oldCode} menü megszűnt, az általad leadott rendelés átkerült a(z) {target.Code} menüre."),
                    performedByUserId,
                    nowUtc);
            }
        }

        return affectedOrders.Select(o => o.Id).ToList();
    }
}
