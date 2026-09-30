using EbedrendeloApp.Features.Notifications.MarkAllNotificationsRead;

namespace EbedrendeloApp.Tests.Features.Notifications;

public class MarkAllNotificationsReadHandlerTests : NotificationReadStateTestBase
{
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
