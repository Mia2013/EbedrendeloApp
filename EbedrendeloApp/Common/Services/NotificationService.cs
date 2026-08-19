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
}
