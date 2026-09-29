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
        listQuery = StoreQuery;
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<IAppClock>(new FixedAppClock(NowLocal));
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Dolgozó Teszt", isAdmin: false));
        Services.AddSingleton<IMediator>(mediator);
        Services.AddScoped<NotificationBadgeState>();

        // A fake egy memóriabeli listán dolgozik, hogy a jelölés hatása a következő lekérdezésben látsszon.
        mediator.Register<GetMyNotificationsQuery, IReadOnlyList<NotificationDto>>(q => listQuery(q));
        mediator.Register<GetNotificationCountsQuery, NotificationCountsDto>(_ =>
            new NotificationCountsDto(store.Count, store.Count(n => !n.IsRead)));

        // Élesben a NotificationBadgeRefreshBehavior frissíti a számlálót a sikeres parancs után — a fake
        // mediatoron nincs pipeline, ezért a parancs-kezelők ezt maguk teszik meg.
        mediator.Register<MarkNotificationReadCommand, Result>(async c =>
        {
            markReadCalls.Add(c.NotificationId);
            MarkRead(c.NotificationId);
            await Services.GetRequiredService<NotificationBadgeState>().RefreshIfDisplayedAsync();
            return Result.Success();
        });
        mediator.Register<MarkAllNotificationsReadCommand, Result<int>>(async _ =>
        {
            var unread = store.Where(n => !n.IsRead).Select(n => n.Id).ToList();
            unread.ForEach(MarkRead);
            await Services.GetRequiredService<NotificationBadgeState>().RefreshIfDisplayedAsync();
            return Result.Success(unread.Count);
        });
    }

    private readonly List<int> markReadCalls = [];

    private Func<GetMyNotificationsQuery, Task<IReadOnlyList<NotificationDto>>> listQuery;

    private Task<IReadOnlyList<NotificationDto>> StoreQuery(GetMyNotificationsQuery q)
        => Task.FromResult<IReadOnlyList<NotificationDto>>(store.Where(n => !q.UnreadOnly || !n.IsRead).ToList());

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

    private static AngleSharp.Dom.IElement Row(IRenderedComponent<MyNotifications> cut, string title)
        => cut.FindAll("button[type='button']").Single(b => b.TextContent.Contains(title));

    private static void ChooseFilter(IRenderedComponent<MyNotifications> cut, string label)
        => cut.FindAll(".mud-chip").First(c => c.TextContent.Contains(label)).Click();

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
    public void Clicking_an_unread_notification_marks_it_read_and_keeps_it_in_place()
    {
        Add(1, "Lemondás", NowLocal);

        var cut = RenderPage();
        Row(cut, "Lemondás").Click();

        cut.WaitForAssertion(() => Assert.Equal("true", Row(cut, "Lemondás").GetAttribute("aria-disabled")));
        Assert.True(store.Single().IsRead);
        Assert.Contains("Olvasatlan (0)", cut.Markup);
    }

    [Fact]
    public void A_second_click_does_not_mark_the_row_that_moved_up_in_the_unread_filter()
    {
        // Dupla kattintás / nyomva tartott Enter: a második esemény nem jelölheti a következő sort.
        Add(1, "Első", NowLocal);
        Add(2, "Második", NowLocal.AddMinutes(-1));

        var cut = RenderPage();
        ChooseFilter(cut, "Olvasatlan");
        cut.WaitForAssertion(() => Assert.Contains("Olvasatlan (2)", cut.Markup));

        var first = Row(cut, "Első");
        first.Click();
        cut.WaitForAssertion(() => Assert.Equal("true", Row(cut, "Első").GetAttribute("aria-disabled")));
        Row(cut, "Első").Click();

        Assert.Equal([1], markReadCalls);
        Assert.False(store.Single(n => n.Id == 2).IsRead);
    }

    [Fact]
    public void The_row_is_a_native_button_whose_accessible_name_is_its_content()
    {
        Add(1, "Rendelésed lemondásra került", NowLocal);

        var cut = RenderPage();
        var row = Row(cut, "Rendelésed lemondásra került");

        // Nincs aria-label, ami elfedné a szöveget és a dátumot; az olvasatlanság szöveggel is szerepel.
        Assert.Null(row.GetAttribute("aria-label"));
        Assert.Contains("Szöveg", row.TextContent);
        Assert.Contains("Olvasatlan · 2026.09.10. 09:00", row.TextContent);
    }

    [Fact]
    public void A_slower_earlier_filter_load_does_not_overwrite_the_later_one()
    {
        Add(1, "Olvasott", NowLocal, isRead: true);
        Add(2, "Új", NowLocal);
        var cut = RenderPage();

        // Az „Olvasatlan" betöltése beragad, közben a felhasználó visszavált „Mind"-re.
        var slowUnread = new TaskCompletionSource<IReadOnlyList<NotificationDto>>();
        listQuery = q => q.UnreadOnly ? slowUnread.Task : StoreQuery(q);
        ChooseFilter(cut, "Olvasatlan");
        ChooseFilter(cut, "Mind");
        cut.WaitForAssertion(() => Assert.Contains(">Olvasott<", cut.Markup));

        cut.InvokeAsync(() => slowUnread.SetResult([store[1]]));

        cut.WaitForAssertion(() => Assert.Contains(">Olvasott<", cut.Markup));
    }

    [Fact]
    public void A_new_notification_found_by_a_badge_refresh_reloads_the_list()
    {
        // Pl. a csengőre kattintás ezen az oldalon: a számláló frissül, és a lista is vele.
        Add(1, "Régi", NowLocal);
        var cut = RenderPage();

        Add(2, "Friss", NowLocal);
        cut.InvokeAsync(() => Services.GetRequiredService<NotificationBadgeState>().RefreshAsync());

        cut.WaitForAssertion(() => Assert.Contains("Friss", cut.Markup));
        Assert.Contains("Mind (2)", cut.Markup);
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
        ChooseFilter(cut, "Olvasatlan");

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
