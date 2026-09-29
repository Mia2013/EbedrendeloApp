using Bunit;
using EbedrendeloApp.Common.Notifications;
using EbedrendeloApp.Features.Notifications;
using EbedrendeloApp.Features.Notifications.GetNotificationCounts;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace EbedrendeloApp.Tests.Common.Notifications;

/// <summary>AC 8.1.2 — az olvasatlan-számláló közös állapota. A csengő megjelenítését a
/// <c>NotificationBellTests</c> fedi; itt a frissítési szabályok: egy lekérdezés több kijelzőre,
/// frissítés navigáláskor és felhasználóváltáskor, és leiratkozás a körrel együtt.</summary>
public class NotificationBadgeStateTests : BunitContext
{
    private readonly FakeMediator mediator = new();
    private readonly FakeCurrentUser currentUser = new(1, "Dolgozó Teszt", isAdmin: false);
    private readonly List<int> queriedUserIds = [];
    private int unread;

    public NotificationBadgeStateTests()
    {
        mediator.Register<GetNotificationCountsQuery, NotificationCountsDto>(q =>
        {
            queriedUserIds.Add(q.UserId);
            return new NotificationCountsDto(unread, unread);
        });
    }

    private NavigationManager Navigation => Services.GetRequiredService<NavigationManager>();

    private NotificationBadgeState CreateSut()
        => new(mediator, currentUser, Navigation, NullLogger<NotificationBadgeState>.Instance);

    [Fact]
    public async Task Two_displays_loading_at_once_share_a_single_query()
    {
        unread = 4;
        using var sut = CreateSut();

        await Task.WhenAll(sut.EnsureLoadedAsync(), sut.EnsureLoadedAsync());

        Assert.Single(queriedUserIds);
        Assert.Equal(4, sut.Unread);
    }

    [Fact]
    public async Task A_failed_load_keeps_the_count_at_zero_and_the_next_call_retries()
    {
        var calls = 0;
        mediator.Register<GetNotificationCountsQuery, NotificationCountsDto>(_ =>
        {
            calls++;
            return calls == 1
                ? throw new InvalidOperationException("átmeneti hiba")
                : new NotificationCountsDto(2, 2);
        });
        using var sut = CreateSut();

        await sut.EnsureLoadedAsync();
        Assert.Equal(0, sut.Unread);

        await sut.EnsureLoadedAsync();
        Assert.Equal(2, sut.Unread);
    }

    [Fact]
    public async Task Navigating_refreshes_the_count_and_raises_Changed()
    {
        unread = 1;
        using var sut = CreateSut();
        await sut.EnsureLoadedAsync();
        var changed = 0;
        sut.Changed += () => changed++;

        unread = 6;
        Navigation.NavigateTo("ertesiteseim");

        Assert.Equal(6, sut.Unread);
        Assert.Equal(1, changed);
    }

    [Fact]
    public async Task Switching_user_reloads_the_count_for_the_new_user()
    {
        using var sut = CreateSut();
        await sut.EnsureLoadedAsync();

        await currentUser.SwitchToAsync(7);

        Assert.Equal([1, 7], queriedUserIds);
    }

    [Fact]
    public async Task A_slower_older_refresh_does_not_overwrite_a_newer_result()
    {
        // Pl. navigálás közben „összes olvasottnak jelölés": a navigáláskor indult (régi, 5-ös) lekérdezés
        // a jelölés utáni (0-s) frissítés után tér vissza — nem írhatja vissza az 5-öt.
        var slow = new TaskCompletionSource<NotificationCountsDto>();
        var calls = 0;
        mediator.Register<GetNotificationCountsQuery, NotificationCountsDto>(_ =>
            ++calls == 1 ? slow.Task : Task.FromResult(new NotificationCountsDto(0, 0)));
        using var sut = CreateSut();

        var older = sut.RefreshAsync();
        await sut.RefreshAsync();
        slow.SetResult(new NotificationCountsDto(5, 5));
        await older;

        Assert.Equal(0, sut.Unread);
    }

    [Fact]
    public async Task RefreshSafelyAsync_logs_instead_of_throwing_and_keeps_the_previous_count()
    {
        unread = 3;
        using var sut = CreateSut();
        await sut.EnsureLoadedAsync();
        mediator.Register<GetNotificationCountsQuery, NotificationCountsDto>(
            (Func<GetNotificationCountsQuery, NotificationCountsDto>)(_ => throw new InvalidOperationException("átmeneti hiba")));

        await sut.RefreshSafelyAsync();

        Assert.Equal(3, sut.Unread);
    }

    [Fact]
    public async Task After_dispose_navigation_no_longer_queries()
    {
        var sut = CreateSut();
        await sut.EnsureLoadedAsync();

        sut.Dispose();
        Navigation.NavigateTo("ertesiteseim");
        await currentUser.SwitchToAsync(7);

        Assert.Single(queriedUserIds);
    }
}
