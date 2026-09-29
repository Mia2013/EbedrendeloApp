using Bunit;
using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Components.Layout;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace EbedrendeloApp.Tests.Components.Layout;

public class MainLayoutTests : MudBunitContext
{
    public MainLayoutTests()
    {
        Services.AddMudServices();
        Services.AddNotificationBadge();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private void UseUser(string displayName, bool isAdmin = false)
    {
        var user = new FakeCurrentUser(1, displayName, isAdmin);
        Services.AddSingleton<ICurrentUser>(user);
        Services.AddSingleton<IDevUserSwitcher>(user);
    }

    [Fact]
    public void Shows_the_app_name_and_the_signed_in_user()
    {
        UseUser("Kovács János");

        var cut = Render<MainLayout>();

        Assert.Contains("Ebédrendelő", cut.Markup);
        Assert.Contains("Kovács János", cut.Markup);
    }

    [Theory]
    [InlineData("Kovács János", "KJ")]
    [InlineData("Nagy Anna Mária", "NA")]  // legfeljebb két kezdőbetű
    [InlineData("Teszt", "T")]
    public void Builds_the_avatar_initials_from_at_most_two_name_parts(string displayName, string expected)
    {
        UseUser(displayName);

        var cut = Render<MainLayout>();

        Assert.Contains(expected, cut.Find("div.mud-avatar").TextContent);
    }

    [Theory]
    [InlineData(true, "Admin")]
    [InlineData(false, "Dolgozó")]
    public void Labels_the_user_by_role(bool isAdmin, string expectedLabel)
    {
        UseUser("Teszt Felhasználó", isAdmin);

        var cut = Render<MainLayout>();

        Assert.Contains(expectedLabel, cut.Markup);
    }

    [Fact]
    public void The_blazor_error_banner_speaks_hungarian()
    {
        UseUser("Teszt Felhasználó");

        var cut = Render<MainLayout>();

        var banner = cut.Find("#blazor-error-ui");
        Assert.Contains("Váratlan hiba történt", banner.TextContent);
        Assert.Contains("Újratöltés", banner.TextContent);
    }

    [Fact]
    public void Renders_the_page_body_inside_the_layout()
    {
        UseUser("Teszt Felhasználó");

        var cut = Render<MainLayout>(p => p.Add(x => x.Body, "<p id=\"probe\">Oldal tartalma</p>"));

        Assert.Equal("Oldal tartalma", cut.Find("#probe").TextContent);
    }
}
