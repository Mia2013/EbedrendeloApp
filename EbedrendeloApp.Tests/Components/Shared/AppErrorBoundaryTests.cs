using Bunit;
using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Components.Shared;
using EbedrendeloApp.Tests.TestSupport;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using MudBlazor.Services;

namespace EbedrendeloApp.Tests.Components.Shared;

public class AppErrorBoundaryTests : MudBunitContext
{
    public AppErrorBoundaryTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private IRenderedComponent<AppErrorBoundary> RenderThrowing(Exception exception)
        => Render<AppErrorBoundary>(parameters => parameters.Add(
            p => p.ChildContent,
            (RenderFragment)((RenderTreeBuilder _) => throw exception)));

    [Fact]
    public void Renders_the_child_when_nothing_throws()
    {
        var cut = Render<AppErrorBoundary>(parameters => parameters.AddChildContent("<p>Rendben</p>"));

        Assert.Contains("Rendben", cut.Markup);
    }

    [Fact]
    public void Shows_the_forbidden_message_verbatim()
    {
        // A ForbiddenException üzenete a mi szövegünk, megmutatható.
        var cut = RenderThrowing(new ForbiddenException("Ehhez a művelethez adminisztrátori jogosultság szükséges."));

        Assert.Contains("Nincs jogosultságod ehhez", cut.Markup);
        Assert.Contains("adminisztrátori jogosultság szükséges", cut.Markup);
    }

    [Fact]
    public void Shows_the_validation_messages()
    {
        var cut = RenderThrowing(new ValidationException([new ValidationFailure("AmountHuf", "Az összegnek pozitívnak kell lennie.")]));

        Assert.Contains("Hibás adat", cut.Markup);
        Assert.Contains("Az összegnek pozitívnak kell lennie.", cut.Markup);
    }

    [Fact]
    public void Hides_the_details_of_an_unexpected_exception()
    {
        // Egy váratlan kivétel üzenete belső részletet szivárogtathat (SQL, útvonal), ezért csak
        // általános szöveget mutatunk.
        var cut = RenderThrowing(new InvalidOperationException("Invalid column name 'Foo' in table Users."));

        Assert.Contains("Valami hiba történt", cut.Markup);
        Assert.DoesNotContain("Invalid column name", cut.Markup);
    }

    [Fact]
    public void Offers_a_way_out_instead_of_a_dead_page()
    {
        var cut = RenderThrowing(new ForbiddenException("Nem szabad."));

        var buttons = cut.FindAll("button").Select(b => b.TextContent.Trim()).ToList();
        Assert.Contains(buttons, b => b.Contains("Újrapróbálom"));
        Assert.Contains(buttons, b => b.Contains("Kezdőlap"));
    }
}
