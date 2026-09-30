using Bunit;
using EbedrendeloApp.Common.Notifications;
using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Components.Layout;
using EbedrendeloApp.Features.Notifications;
using EbedrendeloApp.Features.Notifications.GetNotificationCounts;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace EbedrendeloApp.Tests.Components.Layout;

public class NotificationBellTests : MudBunitContext
{
    private readonly FakeMediator mediator;
    private int unread;

    public NotificationBellTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Dolgozó Teszt", isAdmin: false));
        mediator = Services.AddNotificationBadge();
        mediator.Register<GetNotificationCountsQuery, NotificationCountsDto>(_ => new NotificationCountsDto(unread, unread));
    }

    private IRenderedComponent<NotificationBell> RenderBell()
        => Render<NotificationBell>((ComponentParameterCollectionBuilder<NotificationBell> _) => { });

    [Fact]
    public void Links_to_the_notifications_page()
    {
        var cut = RenderBell();

        Assert.NotNull(cut.Find("a[href='ertesiteseim']"));
    }

    [Fact]
    public void Shows_the_unread_count_in_the_badge_and_the_accessible_label()
    {
        unread = 3;

        var cut = RenderBell();

        Assert.Contains("3", cut.Find(".mud-badge").TextContent);
        Assert.Equal("Értesítések, 3 olvasatlan", cut.Find("a[href='ertesiteseim']").GetAttribute("aria-label"));
    }

    [Fact]
    public void Hides_the_badge_when_everything_is_read()
    {
        unread = 0;

        var cut = RenderBell();

        Assert.Empty(cut.FindAll(".mud-badge"));
    }

    [Fact]
    public void A_failed_first_load_does_not_break_the_bell_and_is_retried()
    {
        var calls = 0;
        mediator.Register<GetNotificationCountsQuery, NotificationCountsDto>(_ =>
        {
            calls++;
            return calls == 1
                ? throw new InvalidOperationException("átmeneti hiba")
                : new NotificationCountsDto(5, 5);
        });

        var first = RenderBell();
        Assert.Empty(first.FindAll(".mud-badge"));

        // A következő kijelző (pl. új navigálás utáni újrarenderelés) újrapróbálja, nem a hibás Task-ot kapja.
        var second = RenderBell();

        second.WaitForAssertion(() => Assert.Contains("5", second.Find(".mud-badge").TextContent));
    }

    [Fact]
    public void Updates_when_the_shared_count_is_refreshed()
    {
        unread = 2;
        var cut = RenderBell();

        unread = 0;
        cut.InvokeAsync(() => Services.GetRequiredService<NotificationBadgeState>().RefreshAsync());

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".mud-badge")));
    }
}
