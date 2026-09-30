using EbedrendeloApp.Common.Notifications;
using EbedrendeloApp.Features.Notifications;
using EbedrendeloApp.Features.Notifications.GetNotificationCounts;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace EbedrendeloApp.Tests.TestSupport;

/// <summary>
/// A layout (menü + fejléc-csengő) minden tesztje igényli az olvasatlan-számlálót. Ez regisztrálja a
/// <see cref="NotificationBadgeState"/>-et és egy <see cref="FakeMediator"/>-t, ami a számláló
/// lekérdezésére <paramref name="unread"/>-et ad. A visszaadott mediatorra további kezelő regisztrálható,
/// vagy a számláló-kezelő felülírható (a <c>Register</c> felülírja az azonos típusút).
/// </summary>
public static class NotificationBadgeTestServices
{
    public static FakeMediator AddNotificationBadge(this IServiceCollection services, int unread = 0)
    {
        var mediator = new FakeMediator();
        mediator.Register<GetNotificationCountsQuery, NotificationCountsDto>(_ => new NotificationCountsDto(unread, unread));
        services.AddSingleton<IMediator>(mediator);
        services.AddScoped<NotificationBadgeState>();
        return mediator;
    }
}
