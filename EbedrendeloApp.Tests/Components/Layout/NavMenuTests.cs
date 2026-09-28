using Bunit;
using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Components.Layout;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace EbedrendeloApp.Tests.Components.Layout;

public class NavMenuTests : EbedrendeloApp.Tests.TestSupport.MudBunitContext
{
    public NavMenuTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Admin_sees_the_admin_links_plus_every_worker_ordering_link()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));

        var cut = Render<NavMenu>((Bunit.ComponentParameterCollectionBuilder<NavMenu> _) => { });

        Assert.Contains("Rendelési időszakok", cut.Markup);
        Assert.Contains("Nem rendelhető napok", cut.Markup);
        Assert.Contains("Rendelések", cut.Markup);
        Assert.Contains("Konyha", cut.Markup);
        Assert.Contains("Ételek", cut.Markup);
        Assert.Contains("Napi kínálat", cut.Markup);
        Assert.Contains("Konyhai lista", cut.Markup);
        Assert.Contains("Számlák", cut.Markup);

        // The admin should be able to order for themselves too — every worker-facing link must also appear.
        Assert.Contains("Naptár", cut.Markup);
        Assert.Contains("Rendeléseim", cut.Markup);
        Assert.Contains("Mai menü", cut.Markup);
        Assert.Contains("Számláim", cut.Markup);
    }

    /// <summary>A csoportosítás lényege, hogy az admin oldalak négy nyitható csoportba kerülnek —
    /// ha valaki visszalapítaná a menüt, ez a teszt bukik.</summary>
    [Fact]
    public void Admin_menu_groups_the_pages_instead_of_listing_them_flat()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));

        var cut = Render<NavMenu>((Bunit.ComponentParameterCollectionBuilder<NavMenu> _) => { });

        Assert.Equal(4, cut.FindAll(".mud-nav-group").Count);
    }

    /// <summary>Az admin oldalt megnyitva a hozzá tartozó csoport nyitva van, a többi csukva —
    /// különben a felhasználónak minden navigálás után kézzel kellene kinyitogatnia a menüt.</summary>
    [Fact]
    public void The_group_of_the_current_page_is_expanded_and_the_others_are_not()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));

        var navigation = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
        navigation.NavigateTo("szamlak");

        var cut = Render<NavMenu>((Bunit.ComponentParameterCollectionBuilder<NavMenu> _) => { });

        var expandedGroups = cut.FindAll(".mud-nav-group .mud-nav-link.mud-expanded");
        Assert.Single(expandedGroups);
        Assert.Contains("Pénzügy", expandedGroups[0].TextContent);
    }

    [Fact]
    public void Worker_sees_only_the_calendar_link()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(2, "Dolgozó Teszt", isAdmin: false));

        var cut = Render<NavMenu>((Bunit.ComponentParameterCollectionBuilder<NavMenu> _) => { });

        Assert.Contains("Naptár", cut.Markup);
        Assert.Contains("Rendeléseim", cut.Markup);
        Assert.Contains("Számláim", cut.Markup);
        Assert.DoesNotContain("Rendelési időszakok", cut.Markup);
        Assert.DoesNotContain("Nem rendelhető napok", cut.Markup);
        Assert.DoesNotContain("Rendelések<", cut.Markup);
        Assert.DoesNotContain("Számlák<", cut.Markup);
        Assert.Empty(cut.FindAll(".mud-nav-group"));
    }
}
