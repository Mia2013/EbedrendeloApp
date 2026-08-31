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
    public void Admin_sees_the_calendar_admin_links_but_not_the_worker_calendar_link()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));

        var cut = Render<NavMenu>((Bunit.ComponentParameterCollectionBuilder<NavMenu> _) => { });

        Assert.Contains("Rendelési időszakok", cut.Markup);
        Assert.Contains("Nem rendelhető napok", cut.Markup);
        Assert.Contains("Rendelések", cut.Markup);
        Assert.DoesNotContain("Naptár", cut.Markup);
        Assert.DoesNotContain("Rendeléseim", cut.Markup);
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
