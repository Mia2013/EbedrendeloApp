using Bunit;
using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Components.Pages;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.AspNetCore.Hosting;
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

    private void SetUp(FakeCurrentUser user, FakeHostEnvironment environment)
    {
        Services.AddSingleton<ICurrentUser>(user);
        Services.AddSingleton<IDevUserSwitcher>(user);
        Services.AddSingleton<IWebHostEnvironment>(environment);
    }

    [Fact]
    public void Shows_the_quick_link_to_the_admin_periods_page_for_an_admin()
    {
        SetUp(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true), FakeHostEnvironment.Development);

        var cut = Render<Home>((Bunit.ComponentParameterCollectionBuilder<Home> _) => { });

        Assert.Contains("Rendelési időszakok megnyitása", cut.Markup);
        Assert.Contains("Admin Teszt", cut.Markup);
    }

    [Fact]
    public void Shows_the_quick_link_to_the_worker_calendar_for_a_non_admin()
    {
        SetUp(new FakeCurrentUser(2, "Dolgozó Teszt", isAdmin: false), FakeHostEnvironment.Development);

        var cut = Render<Home>((Bunit.ComponentParameterCollectionBuilder<Home> _) => { });

        Assert.Contains("Naptár megnyitása", cut.Markup);
    }

    [Fact]
    public void Shows_the_dev_user_switcher_in_development()
    {
        SetUp(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true), FakeHostEnvironment.Development);

        var cut = Render<Home>((Bunit.ComponentParameterCollectionBuilder<Home> _) => { });

        Assert.Contains("Fejlesztői felhasználóváltó", cut.Markup);
    }

    [Fact]
    public void Hides_the_dev_user_switcher_outside_development()
    {
        // A váltó ellenőrzés nélkül vesz fel tetszőleges identitást — éles környezetben ez maga lenne
        // a jogosultság-kerülő út.
        SetUp(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true), FakeHostEnvironment.Production);

        var cut = Render<Home>((Bunit.ComponentParameterCollectionBuilder<Home> _) => { });

        Assert.DoesNotContain("Fejlesztői felhasználóváltó", cut.Markup);
    }
}
