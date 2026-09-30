using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Notifications;
using EbedrendeloApp.Features.Notifications.GetNotificationCounts;
using EbedrendeloApp.Tests.TestSupport;

namespace EbedrendeloApp.Tests.Features.Notifications;

/// <summary>Az olvasottság use case-einek (számláló, egy jelölése, mind jelölése) közös seedje: két
/// felhasználó és értesítés-felvitel. A jelölés-tesztek a számlálóval ellenőrzik a hatásukat.</summary>
public abstract class NotificationReadStateTestBase : IDisposable
{
    // 2026-09-10 is a Thursday.
    private static readonly DateTime NowLocal = new(2026, 9, 10, 9, 0, 0);

    protected readonly SqliteDbContextFactory dbFactory = new();
    protected readonly FixedAppClock clock = new(NowLocal);
    protected readonly int userId;
    protected readonly int otherUserId;

    protected NotificationReadStateTestBase()
    {
        using var db = dbFactory.CreateDbContext();
        var role = new Role { Name = "User" };
        db.Roles.Add(role);
        db.SaveChanges();

        var user = new User { UserId = 1, UserName = "u1", RoleId = role.Id };
        var other = new User { UserId = 2, UserName = "u2", RoleId = role.Id };
        db.Users.AddRange(user, other);
        db.SaveChanges();
        userId = user.Id;
        otherUserId = other.Id;
    }

    public void Dispose() => dbFactory.Dispose();

    protected int SeedNotification(int ownerUserId, bool read = false)
    {
        using var db = dbFactory.CreateDbContext();
        var notification = new UserNotification
        {
            UserId = ownerUserId,
            Type = NotificationType.CreditIssued,
            Title = "Jóváírás érkezett",
            Message = "Szöveg",
            CreatedAtUtc = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc),
            ReadAtUtc = read ? new DateTime(2026, 9, 2, 8, 0, 0, DateTimeKind.Utc) : null,
        };
        db.UserNotifications.Add(notification);
        db.SaveChanges();
        return notification.Id;
    }

    protected Task<NotificationCountsDto> CountsAsync(int forUserId)
        => new GetNotificationCountsHandler(dbFactory).Handle(new GetNotificationCountsQuery(forUserId), CancellationToken.None);
}
