using Bunit;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Components.Shared;
using EbedrendeloApp.Features.Users.GetUsers;
using EbedrendeloApp.Features.Users.ResolveColleague;
using EbedrendeloApp.Tests.TestSupport;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace EbedrendeloApp.Tests.Components.Shared;

public class ColleaguePickerTests : MudBunitContext
{
    private static readonly UserOptionDto Colleague = new(2, "kanna", 2, "Kovács Anna", "User", "Gyártás", "Logisztika");

    private readonly FakeMediator mediator = new();

    public ColleaguePickerTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<IMediator>(mediator);
    }

    private IRenderedComponent<ColleaguePicker> RenderPicker(bool isAdmin, UserOptionDto? selected = null)
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Teszt", isAdmin));
        return Render<ColleaguePicker>(parameters => parameters.Add(p => p.Selected, selected));
    }

    [Fact]
    public void A_worker_gets_no_wording_that_offers_ordering_for_someone_else()
    {
        // Szándékos: a funkció létezik, de a felület nem kínálja fel. Csak egy felirat nélküli,
        // semleges ikon van — aki tudja, hogy van ilyen, megtalálja.
        var cut = RenderPicker(isAdmin: false);

        Assert.DoesNotContain("nevében", cut.Markup);
        Assert.DoesNotContain("Kolléga neve", cut.Markup); // az űrlap alapból csukva van
        Assert.Single(cut.FindAll("button"));
    }

    [Fact]
    public void A_worker_must_give_all_three_fields_before_the_search_is_enabled()
    {
        var cut = RenderPicker(isAdmin: false);
        cut.Find("button").Click();

        var searchButton = cut.FindAll("button").First(b => b.TextContent.Contains("Kolléga keresése"));
        Assert.True(searchButton.HasAttribute("disabled"));

        var fields = cut.FindComponents<MudTextField<string>>();
        Assert.Equal(3, fields.Count);
        foreach (var field in fields)
        {
            cut.InvokeAsync(() => field.Instance.ValueChanged.InvokeAsync("x")).GetAwaiter().GetResult();
        }

        searchButton = cut.FindAll("button").First(b => b.TextContent.Contains("Kolléga keresése"));
        Assert.False(searchButton.HasAttribute("disabled"));
    }

    [Fact]
    public async Task A_successful_resolve_reports_the_colleague_upwards()
    {
        ResolveColleagueQuery? sentQuery = null;
        mediator.Register<ResolveColleagueQuery, Result<UserOptionDto>>(q =>
        {
            sentQuery = q;
            return Result.Success(Colleague);
        });

        UserOptionDto? reported = null;
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Teszt", isAdmin: false));
        var cut = Render<ColleaguePicker>(parameters => parameters
            .Add(p => p.Selected, (UserOptionDto?)null)
            .Add(p => p.SelectedChanged, (UserOptionDto? u) => reported = u));

        cut.Find("button").Click();
        foreach (var (field, value) in cut.FindComponents<MudTextField<string>>().Zip(new[] { "Kovács Anna", "Gyártás", "Logisztika" }))
        {
            await cut.InvokeAsync(() => field.Instance.ValueChanged.InvokeAsync(value));
        }

        var searchButton = cut.FindAll("button").First(b => b.TextContent.Contains("Kolléga keresése"));
        await cut.InvokeAsync(() => searchButton.Click());

        Assert.NotNull(sentQuery);
        Assert.Equal("Kovács Anna", sentQuery!.Name);
        Assert.Equal("Gyártás", sentQuery.Igazgatosag);
        Assert.Equal("Logisztika", sentQuery.Osztaly);
        Assert.Equal(Colleague.Id, reported?.Id);
    }

    [Fact]
    public async Task A_failed_resolve_shows_the_servers_neutral_message_and_selects_nobody()
    {
        var called = false;
        mediator.Register<ResolveColleagueQuery, Result<UserOptionDto>>(
            _ => Result.Failure<UserOptionDto>(ErrorCodes.NotFound, "Nincs ilyen dolgozó."));

        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Teszt", isAdmin: false));
        var cut = Render<ColleaguePicker>(parameters => parameters
            .Add(p => p.Selected, (UserOptionDto?)null)
            .Add(p => p.SelectedChanged, (UserOptionDto? _) => called = true));

        cut.Find("button").Click();
        foreach (var (field, value) in cut.FindComponents<MudTextField<string>>().Zip(new[] { "Nagy Béla", "Gyártás", "Logisztika" }))
        {
            await cut.InvokeAsync(() => field.Instance.ValueChanged.InvokeAsync(value));
        }

        var searchButton = cut.FindAll("button").First(b => b.TextContent.Contains("Kolléga keresése"));
        await cut.InvokeAsync(() => searchButton.Click());

        Assert.Contains("Nincs ilyen dolgozó.", cut.Markup);
        Assert.False(called);
    }

    [Fact]
    public void An_admin_gets_an_autocomplete_instead_of_the_three_field_form()
    {
        // Az adminnak amúgy is joga van a teljes névsorhoz, és neki ez napi munka.
        mediator.Register<GetUsersQuery, Result<IReadOnlyList<UserOptionDto>>>(
            _ => Result.Success<IReadOnlyList<UserOptionDto>>([Colleague]));

        var cut = RenderPicker(isAdmin: true);
        cut.FindAll("button").First(b => b.TextContent.Contains("Másik dolgozó naptára")).Click();

        Assert.NotNull(cut.FindComponent<MudAutocomplete<UserOptionDto>>());
        Assert.DoesNotContain("Kolléga keresése", cut.Markup);
    }

    [Fact]
    public void A_selected_colleague_is_shown_with_a_way_back_to_my_own_calendar()
    {
        var cut = RenderPicker(isAdmin: false, selected: Colleague);

        Assert.Contains("Kovács Anna", cut.Markup);
        Assert.Contains("Vissza a saját naptáramhoz", cut.Markup);
    }
}
