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
        Assert.Contains("À la carte ételek", cut.Markup);
        Assert.Contains("À la carte napi kínálat", cut.Markup);
        Assert.Contains("À la carte konyhai lista", cut.Markup);

        // The admin should be able to order for themselves too — every worker-facing link must also appear.
        Assert.Contains("Naptár", cut.Markup);
        Assert.Contains("Rendeléseim", cut.Markup);
        Assert.Contains("Mai menü", cut.Markup);
    }

    [Fact]
    public void Worker_sees_only_the_calendar_link()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(2, "Dolgozó Teszt", isAdmin: false));

        var cut = Render<NavMenu>((Bunit.ComponentParameterCollectionBuilder<NavMenu> _) => { });

        Assert.Contains("Naptár", cut.Markup);
        Assert.Contains("Rendeléseim", cut.Markup);
        Assert.DoesNotContain("Rendelési időszakok", cut.Markup);
        Assert.DoesNotContain("Nem rendelhető napok", cut.Markup);
        Assert.DoesNotContain("Rendelések<", cut.Markup);
    }
}
