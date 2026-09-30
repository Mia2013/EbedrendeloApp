using Bunit;
using EbedrendeloApp.Common.Notifications;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Features.Notifications;
using EbedrendeloApp.Features.Notifications.GetNotificationCounts;
using EbedrendeloApp.Features.Notifications.MarkNotificationRead;
using EbedrendeloApp.Features.Orders.PlacePeriodOrder;
using EbedrendeloApp.Tests.TestSupport;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace EbedrendeloApp.Tests.Common.Notifications;

/// <summary>A saját művelet keltette értesítés azonnal látsszon a csengőn: sikeres parancs után a
/// számláló frissül, lekérdezés és sikertelen parancs után nem.</summary>
public class NotificationBadgeRefreshBehaviorTests : BunitContext
{
    private readonly FakeMediator mediator = new();
    private readonly NotificationBadgeState badge;
    private int countQueries;

    public NotificationBadgeRefreshBehaviorTests()
    {
        mediator.Register<GetNotificationCountsQuery, NotificationCountsDto>(_ =>
        {
            countQueries++;
            return new NotificationCountsDto(countQueries, countQueries);
        });
        badge = new NotificationBadgeState(
            mediator,
            new FakeCurrentUser(1, "Dolgozó Teszt", isAdmin: false),
            Services.GetRequiredService<NavigationManager>(),
            NullLogger<NotificationBadgeState>.Instance);
    }

    private static Task<TResponse> Run<TRequest, TResponse>(
        NotificationBadgeState badge, TRequest request, TResponse response)
        where TRequest : notnull
        => new NotificationBadgeRefreshBehavior<TRequest, TResponse>(badge)
            .Handle(request, _ => Task.FromResult(response), CancellationToken.None);

    [Fact]
    public async Task A_successful_command_refreshes_the_displayed_count()
    {
        await badge.EnsureLoadedAsync();

        await Run(badge, new MarkNotificationReadCommand(1, 5), Result.Success());

        Assert.Equal(2, countQueries);
        Assert.Equal(2, badge.Unread);
    }

    [Fact]
    public async Task A_failed_command_does_not_refresh()
    {
        await badge.EnsureLoadedAsync();

        await Run(badge, new MarkNotificationReadCommand(1, 5), Result.Failure(ErrorCodes.NotFound, "nincs"));

        Assert.Equal(1, countQueries);
    }

    [Fact]
    public async Task A_query_does_not_refresh()
    {
        await badge.EnsureLoadedAsync();

        await Run(badge, new GetNotificationCountsQuery(1), new NotificationCountsDto(0, 0));

        Assert.Equal(1, countQueries);
    }

    [Fact]
    public async Task Nothing_is_queried_while_no_display_shows_the_count()
    {
        await Run(badge, new MarkNotificationReadCommand(1, 5), Result.Success());

        Assert.Equal(0, countQueries);
    }

    [Fact]
    public void Every_request_is_named_Command_or_Query()
    {
        // A behavior a névből ismeri fel a parancsot — egy másként elnevezett parancs után csendben
        // elmaradna a csengő frissítése.
        var misnamed = typeof(PlacePeriodOrderCommand).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false }
                        && t.GetInterfaces().Any(i => i == typeof(IBaseRequest)))
            .Where(t => !t.Name.EndsWith("Command", StringComparison.Ordinal) && !t.Name.EndsWith("Query", StringComparison.Ordinal))
            .Select(t => t.FullName)
            .ToList();

        Assert.Empty(misnamed);
    }
}
