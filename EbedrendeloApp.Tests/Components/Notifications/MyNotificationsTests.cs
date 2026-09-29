using Bunit;
using EbedrendeloApp.Common.Notifications;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Common.Time;
using EbedrendeloApp.Components.Pages.Notifications;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Notifications;
using EbedrendeloApp.Features.Notifications.GetMyNotifications;
using EbedrendeloApp.Features.Notifications.GetNotificationCounts;
using EbedrendeloApp.Features.Notifications.MarkAllNotificationsRead;
using EbedrendeloApp.Features.Notifications.MarkNotificationRead;
using EbedrendeloApp.Tests.TestSupport;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace EbedrendeloApp.Tests.Components.Notifications;

public class MyNotificationsTests : MudBunitContext
{
    // 2026-09-10 is a Thursday.
    private static readonly DateTime NowLocal = new(2026, 9, 10, 9, 0, 0);

    private readonly FakeMediator mediator = new();
    private readonly List<NotificationDto> store = [];

    public MyNotificationsTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<IAppClock>(new FixedAppClock(NowLocal));
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Dolgozó Teszt", isAdmin: false));
        Services.AddSingleton<IMediator>(mediator);
        Services.AddScoped<NotificationBadgeState>();

        // A fake egy memóriabeli listán dolgozik, hogy a jelölés hatása a következő lekérdezésben látsszon.
        mediator.Register<GetMyNotificationsQuery, IReadOnlyList<NotificationDto>>(q =>
            store.Where(n => !q.UnreadOnly || !n.IsRead).ToList());
        mediator.Register<GetNotificationCountsQuery, NotificationCountsDto>(_ =>
            new NotificationCountsDto(store.Count, store.Count(n => !n.IsRead)));
        mediator.Register<MarkNotificationReadCommand, Result>(c =>
        {
            MarkRead(c.NotificationId);
            return Result.Success();
        });
        mediator.Register<MarkAllNotificationsReadCommand, Result<int>>(_ =>
        {
            var unread = store.Where(n => !n.IsRead).Select(n => n.Id).ToList();
            unread.ForEach(MarkRead);
            return Result.Success(unread.Count);
        });
    }

    private void MarkRead(int id)
    {
        var index = store.FindIndex(n => n.Id == id);
        store[index] = store[index] with { IsRead = true };
    }

    private void Add(int id, string title, DateTime createdAtLocal, bool isRead = false, string? onBehalfOf = null)
        => store.Add(new NotificationDto(id, NotificationType.MenuCancelled, title, "Szöveg", new DateOnly(2026, 9, 15),
            createdAtLocal, isRead, onBehalfOf));

    private IRenderedComponent<MyNotifications> RenderPage()
        => Render<MyNotifications>((ComponentParameterCollectionBuilder<MyNotifications> _) => { });

    private static IReadOnlyList<string> GroupLabels(IRenderedComponent<MyNotifications> cut)
        => cut.FindAll(".mud-typography-overline").Select(e => e.TextContent.Trim()).ToList();

    [Fact]
    public void Groups_the_notifications_into_today_yesterday_and_earlier()
    {
        Add(1, "Mai", NowLocal.AddHours(-1));
        Add(2, "Tegnapi", NowLocal.AddDays(-1));
        Add(3, "Régi", NowLocal.AddDays(-5));

        var cut = RenderPage();

        Assert.Equal(["Ma", "Tegnap", "Korábban"], GroupLabels(cut));
    }

    [Fact]
    public void Shows_whose_order_a_placer_notification_is_about()
    {
        Add(1, "Az általad leadott rendelés lemondásra került", NowLocal, onBehalfOf: "Nagy Anna");

        var cut = RenderPage();

        Assert.Contains("Nagy Anna nevében", cut.Markup);
    }

    [Fact]
    public void Clicking_an_unread_notification_marks_it_read()
    {
        Add(1, "Olvasatlan", NowLocal);

        var cut = RenderPage();
        Assert.Single(cut.FindAll("[role='button']"));

        cut.Find("[role='button']").Click();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[role='button']")));
        Assert.True(store.Single().IsRead);
    }

    [Fact]
    public void Enter_on_an_unread_notification_marks_it_read_too()
    {
        Add(1, "Olvasatlan", NowLocal);

        var cut = RenderPage();
        cut.Find("[role='button']").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter" });

        cut.WaitForAssertion(() => Assert.True(store.Single().IsRead));
    }

    [Fact]
    public void Mark_all_reads_everything_and_then_shows_the_all_read_state()
    {
        Add(1, "Egy", NowLocal);
        Add(2, "Kettő", NowLocal);

        var cut = RenderPage();
        cut.FindAll("button").First(b => b.TextContent.Contains("Összes olvasottnak jelöl")).Click();

        cut.WaitForAssertion(() => Assert.Contains("Mindent elolvastál.", cut.Markup));
        Assert.All(store, n => Assert.True(n.IsRead));
        Assert.DoesNotContain("Összes olvasottnak jelöl", cut.Markup);
    }

    [Fact]
    public void The_unread_filter_hides_the_read_ones()
    {
        Add(1, "Olvasott", NowLocal, isRead: true);
        Add(2, "Olvasatlan", NowLocal);

        var cut = RenderPage();
        cut.FindAll(".mud-chip").First(c => c.TextContent.Contains("Olvasatlan")).Click();

        cut.WaitForAssertion(() => Assert.DoesNotContain(">Olvasott<", cut.Markup));
        Assert.Contains("Olvasatlan (1)", cut.Markup);
    }

    [Fact]
    public void Without_any_notification_the_empty_state_is_shown()
    {
        var cut = RenderPage();

        Assert.Contains("Még nincs értesítésed.", cut.Markup);
        Assert.Empty(cut.FindAll(".mud-chip"));
    }
}
