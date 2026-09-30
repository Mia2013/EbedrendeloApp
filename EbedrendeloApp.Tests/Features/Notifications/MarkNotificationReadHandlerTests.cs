using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Features.Notifications.MarkNotificationRead;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Tests.Features.Notifications;

public class MarkNotificationReadHandlerTests : NotificationReadStateTestBase
{
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
}
