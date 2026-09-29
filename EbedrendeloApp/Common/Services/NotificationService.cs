using EbedrendeloApp.Data;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;

namespace EbedrendeloApp.Common.Services;

public sealed class NotificationService : INotificationService
{
    public void Notify(
        EbedrendeloDbContext db,
        int userId,
        NotificationType type,
        string title,
        string message,
        DateTime nowUtc,
        DateOnly? relatedDate = null,
        int? relatedMenuOrderId = null)
    {
        db.UserNotifications.Add(new UserNotification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            RelatedDate = relatedDate,
            RelatedMenuOrderId = relatedMenuOrderId,
            CreatedAtUtc = nowUtc,
        });
    }

    public void NotifyOrderParties(
        EbedrendeloDbContext db,
        MenuOrder order,
        NotificationType type,
        OrderNotificationText owner,
        OrderNotificationText placer,
        int performedByUserId,
        DateTime nowUtc)
        => NotifyParties(db, order, type, type, owner, placer, performedByUserId, nowUtc);

    public void NotifyOrderCancelled(
        EbedrendeloDbContext db,
        MenuOrder order,
        CreditEntry? credit,
        OrderNotificationText owner,
        OrderNotificationText placer,
        int performedByUserId,
        DateTime nowUtc)
        => NotifyParties(
            db,
            order,
            credit is null ? NotificationType.MenuCancelled : NotificationType.CreditIssued,
            NotificationType.MenuCancelled,
            owner,
            placer,
            performedByUserId,
            nowUtc);

    private void NotifyParties(
        EbedrendeloDbContext db,
        MenuOrder order,
        NotificationType ownerType,
        NotificationType placerType,
        OrderNotificationText owner,
        OrderNotificationText placer,
        int performedByUserId,
        DateTime nowUtc)
    {
        Notify(db, order.UserId, ownerType, owner.Title, owner.Message, nowUtc, order.Date, order.Id);

        if (order.PlacedByUserId != order.UserId && order.PlacedByUserId != performedByUserId)
        {
            Notify(db, order.PlacedByUserId, placerType, placer.Title, placer.Message, nowUtc, order.Date, order.Id);
        }
    }
}
