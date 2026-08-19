using EbedrendeloApp.Data;
using EbedrendeloApp.Domain.Enums;

namespace EbedrendeloApp.Common.Services;

/// <summary>
/// Adds in-app notifications (01-szerver-architektura.md Epic 8 / US-8.1). Operates on the caller's
/// <see cref="EbedrendeloDbContext"/> and does not call SaveChanges — see <see cref="ICreditService"/>.
/// </summary>
public interface INotificationService
{
    void Notify(
        EbedrendeloDbContext db,
        int userId,
        NotificationType type,
        string title,
        string message,
        DateTime nowUtc,
        DateOnly? relatedDate = null,
        int? relatedMenuOrderId = null);
}
