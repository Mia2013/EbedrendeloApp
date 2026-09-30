using EbedrendeloApp.Features.Notifications;

namespace EbedrendeloApp.Tests.Features.Notifications;

public class GetNotificationCountsHandlerTests : NotificationReadStateTestBase
{
    [Fact]
    public async Task Counts_report_the_total_and_the_unread_of_the_caller_only()
    {
        SeedNotification(userId);
        SeedNotification(userId, read: true);
        SeedNotification(otherUserId);

        var counts = await CountsAsync(userId);

        Assert.Equal(new NotificationCountsDto(Total: 2, Unread: 1), counts);
    }
}
