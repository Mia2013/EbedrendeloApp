using Bunit;
using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Components.Layout;
using EbedrendeloApp.Features.Notifications;
using EbedrendeloApp.Features.Notifications.GetNotificationCounts;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace EbedrendeloApp.Tests.Components.Layout;

public class NavMenuTests : EbedrendeloApp.Tests.TestSupport.MudBunitContext
{
    private static readonly string[] AdminHrefs =
    [
        "admin", "idoszakok", "nem-rendelheto-napok", "etlap", "rendelesek", "konyhai-osszesito",
        "alacarte-etelek", "alacarte-napi-kinalat", "alacarte-konyhai-lista", "egyenlegek", "szamlak",
    ];

    private static readonly string[] WorkerHrefs = ["naptar", "rendeleseim", "mai-menu", "egyenlegem", "szamlaim", "ertesiteseim"];

    private readonly FakeMediator badgeMediator;

    public NavMenuTests()
    {
        Services.AddMudServices();
        badgeMediator = Services.AddNotificationBadge();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private void SetUnread(int unread)
        => badgeMediator.Register<GetNotificationCountsQuery, NotificationCountsDto>(_ => new NotificationCountsDto(unread, unread));

    private static string? UnreadChipText(IRenderedComponent<NavMenu> cut)
        => cut.Find("a[href='ertesiteseim']").QuerySelector(".mud-badge")?.TextContent.Trim();

    /// <summary>A szolgáltatást a navigálás előtt kell regisztrálni: bUnitban az első
    /// <c>GetRequiredService</c> után már nem vehető fel új.</summary>
    private IRenderedComponent<NavMenu> RenderAs(bool isAdmin, string? startUri = null)
    {
        Services.AddSingleton<ICurrentUser>(isAdmin
            ? new FakeCurrentUser(1, "Admin Teszt", isAdmin: true)
            : new FakeCurrentUser(2, "Dolgozó Teszt", isAdmin: false));

        if (startUri is not null)
        {
            Services.GetRequiredService<NavigationManager>().NavigateTo(startUri);
        }

        return Render<NavMenu>((ComponentParameterCollectionBuilder<NavMenu> _) => { });
    }

    // A linkeket href alapján keressük, nem szövegre: a „Konyha" a „Konyhai lista" része, a „Naptár"
    // pedig csoportcím is — szövegre állítva a link törlése sem buktatná el a tesztet.
    private static bool HasLink(IRenderedComponent<NavMenu> cut, string href)
        => cut.FindAll($"a[href='{href}']").Count > 0;

    [Fact]
    public void Admin_sees_the_admin_links_plus_every_worker_ordering_link()
    {
        var cut = RenderAs(isAdmin: true);

        Assert.All(AdminHrefs, href => Assert.True(HasLink(cut, href), $"Hiányzik az admin link: {href}"));

        // Az admin magának is rendel — minden dolgozói linknek meg kell lennie.
        Assert.All(WorkerHrefs, href => Assert.True(HasLink(cut, href), $"Hiányzik a saját rendelés link: {href}"));
    }

    [Fact]
    public void Both_admin_and_worker_can_get_back_to_the_home_page()
    {
        Assert.True(HasLink(RenderAs(isAdmin: true), ""));
    }

    /// <summary>A csoportosítás lényege, hogy az admin oldalak négy nyitható csoportba kerülnek —
    /// ha valaki visszalapítaná a menüt, ez a teszt bukik.</summary>
    [Fact]
    public void Admin_menu_groups_the_pages_instead_of_listing_them_flat()
    {
        var cut = RenderAs(isAdmin: true);

        Assert.Equal(4, cut.FindAll(".mud-nav-group").Count);
    }

    /// <summary>Az admin oldalt megnyitva a hozzá tartozó csoport nyitva van, a többi csukva —
    /// különben a felhasználónak minden navigálás után kézzel kellene kinyitogatnia a menüt.</summary>
    [Theory]
    [InlineData("szamlak")]
    [InlineData("Szamlak")]
    [InlineData("szamlak#fizetetlen")]
    public void The_group_of_the_current_page_is_expanded_and_the_others_are_not(string uri)
    {
        var cut = RenderAs(isAdmin: true, startUri: uri);

        var expandedGroups = cut.FindAll(".mud-nav-group .mud-nav-link.mud-expanded");
        Assert.Single(expandedGroups);
        Assert.Contains("Pénzügy", expandedGroups[0].TextContent);
    }

    [Fact]
    public void Navigating_to_another_group_re_syncs_the_expanded_group()
    {
        var cut = RenderAs(isAdmin: true, startUri: "szamlak");

        Services.GetRequiredService<NavigationManager>().NavigateTo("alacarte-etelek");

        cut.WaitForAssertion(() =>
        {
            var expandedGroups = cut.FindAll(".mud-nav-group .mud-nav-link.mud-expanded");
            Assert.Single(expandedGroups);
            Assert.Contains("À la carte", expandedGroups[0].TextContent);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_notifications_link_shows_the_unread_count(bool isAdmin)
    {
        SetUnread(4);

        var cut = RenderAs(isAdmin);

        cut.WaitForAssertion(() => Assert.Equal("4", UnreadChipText(cut)));
        // A szám felolvasható címkét kap, és nem fókuszálható elem a linken belül.
        var badge = cut.Find("a[href='ertesiteseim'] .mud-badge");
        Assert.Equal("4 olvasatlan", badge.GetAttribute("aria-label"));
        Assert.Empty(cut.FindAll("a[href='ertesiteseim'] [tabindex]"));
    }

    [Fact]
    public void The_links_do_not_wait_for_the_unread_count()
    {
        // A számláló lekérdezése még fut — a szerepkörfüggő linkek ettől már látszanak.
        var pending = new TaskCompletionSource<NotificationCountsDto>();
        badgeMediator.Register<GetNotificationCountsQuery, NotificationCountsDto>(_ => pending.Task);

        var cut = RenderAs(isAdmin: false);

        Assert.NotNull(cut.Find("a[href='naptar']"));
        Assert.Null(UnreadChipText(cut));

        pending.SetResult(new NotificationCountsDto(2, 2));
        cut.WaitForAssertion(() => Assert.Equal("2", UnreadChipText(cut)));
    }

    [Fact]
    public void Without_unread_notifications_the_link_has_no_count()
    {
        SetUnread(0);

        var cut = RenderAs(isAdmin: false);

        Assert.Null(UnreadChipText(cut));
    }

    [Fact]
    public void Worker_sees_only_the_worker_links_flat()
    {
        var cut = RenderAs(isAdmin: false);

        Assert.All(WorkerHrefs, href => Assert.True(HasLink(cut, href), $"Hiányzik a dolgozói link: {href}"));
        Assert.True(HasLink(cut, ""));
        Assert.All(AdminHrefs, href => Assert.False(HasLink(cut, href), $"Dolgozónak nem látható admin link: {href}"));
        Assert.Empty(cut.FindAll(".mud-nav-group"));
    }
}
