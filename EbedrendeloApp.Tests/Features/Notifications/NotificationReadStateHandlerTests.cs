using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Notifications;
using EbedrendeloApp.Features.Notifications.GetNotificationCounts;
using EbedrendeloApp.Features.Notifications.MarkAllNotificationsRead;
using EbedrendeloApp.Features.Notifications.MarkNotificationRead;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Tests.Features.Notifications;

/// <summary>Az olvasottság három use case-e (számláló, egy jelölése, mind jelölése) — ugyanazon a
/// seed-en, mert egymás hatását ellenőrzik.</summary>
public class NotificationReadStateHandlerTests : IDisposable
{
    // 2026-09-10 is a Thursday.
    private static readonly DateTime NowLocal = new(2026, 9, 10, 9, 0, 0);

    private readonly SqliteDbContextFactory dbFactory = new();
    private readonly FixedAppClock clock = new(NowLocal);
    private readonly int userId;
    private readonly int otherUserId;

    public NotificationReadStateHandlerTests()
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

    private int SeedNotification(int ownerUserId, bool read = false)
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

    private Task<NotificationCountsDto> CountsAsync(int forUserId)
        => new GetNotificationCountsHandler(dbFactory).Handle(new GetNotificationCountsQuery(forUserId), CancellationToken.None);

    [Fact]
    public async Task Counts_report_the_total_and_the_unread_of_the_caller_only()
    {
        SeedNotification(userId);
        SeedNotification(userId, read: true);
        SeedNotification(otherUserId);

        var counts = await CountsAsync(userId);

        Assert.Equal(new NotificationCountsDto(Total: 2, Unread: 1), counts);
    }

    [Fact]
    public async Task Marking_one_as_read_stamps_the_time_and_lowers_the_unread_count()
    {
        var id = SeedNotification(userId);

        var result = await new MarkNotificationReadHandler(dbFactory, clock)
            .Handle(new MarkNotificationReadCommand(userId, id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        await using var db = dbFactory.CreateDbContext();
        Assert.NotNull((await db.UserNotifications.SingleAsync(n => n.Id == id)).ReadAtUtc);
        Assert.Equal(0, (await CountsAsync(userId)).Unread);
    }

    [Fact]
    public async Task Marking_an_already_read_one_is_a_successful_no_op()
    {
        var id = SeedNotification(userId, read: true);

        var result = await new MarkNotificationReadHandler(dbFactory, clock)
            .Handle(new MarkNotificationReadCommand(userId, id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        await using var db = dbFactory.CreateDbContext();
        // Az eredeti olvasási idő marad.
        Assert.Equal(new DateTime(2026, 9, 2, 8, 0, 0), (await db.UserNotifications.SingleAsync(n => n.Id == id)).ReadAtUtc);
    }

    [Fact]
    public async Task Someone_elses_notification_is_reported_as_not_found_and_left_untouched()
    {
        var id = SeedNotification(otherUserId);

        var result = await new MarkNotificationReadHandler(dbFactory, clock)
            .Handle(new MarkNotificationReadCommand(userId, id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NotFound, result.ErrorCode);
        Assert.Equal(1, (await CountsAsync(otherUserId)).Unread);
    }

    [Fact]
    public async Task Mark_all_marks_every_unread_including_those_beyond_the_listed_twenty()
    {
        for (var i = 0; i < 25; i++)
        {
            SeedNotification(userId);
        }

        SeedNotification(otherUserId);

        var result = await new MarkAllNotificationsReadHandler(dbFactory, clock)
            .Handle(new MarkAllNotificationsReadCommand(userId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(25, result.Value);
        Assert.Equal(0, (await CountsAsync(userId)).Unread);
        // Más felhasználó értesítéseit nem érinti.
        Assert.Equal(1, (await CountsAsync(otherUserId)).Unread);
    }
}
