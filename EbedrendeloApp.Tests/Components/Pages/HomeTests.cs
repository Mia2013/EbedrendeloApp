using Bunit;
using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Components.Pages;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace EbedrendeloApp.Tests.Components.Pages;

public class HomeTests : EbedrendeloApp.Tests.TestSupport.MudBunitContext
{
    public HomeTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Shows_the_quick_link_to_the_admin_periods_page_for_an_admin()
    {
        var user = new FakeCurrentUser(1, "Admin Teszt", isAdmin: true);
        Services.AddSingleton<ICurrentUser>(user);
        Services.AddSingleton<IDevUserSwitcher>(user);

        var cut = Render<Home>((Bunit.ComponentParameterCollectionBuilder<Home> _) => { });

        Assert.Contains("Rendelési időszakok megnyitása", cut.Markup);
        Assert.Contains("Admin Teszt", cut.Markup);
    }

    [Fact]
    public void Shows_the_quick_link_to_the_worker_calendar_for_a_non_admin()
    {
        var user = new FakeCurrentUser(2, "Dolgozó Teszt", isAdmin: false);
        Services.AddSingleton<ICurrentUser>(user);
        Services.AddSingleton<IDevUserSwitcher>(user);

        var cut = Render<Home>((Bunit.ComponentParameterCollectionBuilder<Home> _) => { });

        Assert.Contains("Naptár megnyitása", cut.Markup);
    }
}
