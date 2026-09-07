using Bunit;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Components.Pages.Billing;
using EbedrendeloApp.Features.Billing.AddManualCredit;
using EbedrendeloApp.Features.Billing.GetMyBalance;
using EbedrendeloApp.Features.Users.GetUsers;
using EbedrendeloApp.Tests.TestSupport;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace EbedrendeloApp.Tests.Components.Billing;

public class ManualCreditDialogTests : MudBunitContext
{
    private static readonly UserOptionDto Kovacs = new(7, "kovacs.j", 1002, "Kovács János", "User", "Gyártás", "1. üzem");
    private static readonly UserOptionDto Nagy = new(8, "nagy.a", 1003, "Nagy Anna", "User", "Gyártás", "1. üzem");

    public ManualCreditDialogTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
    }

    private async Task<IRenderedComponent<MudDialogProvider>> OpenAsync(FakeMediator mediator, int? preselectedUserId = null)
    {
        Services.AddSingleton<IMediator>(mediator);
        var provider = Render<MudDialogProvider>();
        var dialogService = Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<ManualCreditDialog> { { x => x.PreselectedUserId, preselectedUserId } };
        await provider.InvokeAsync(() => dialogService.ShowAsync<ManualCreditDialog>("Új jóváírás", parameters));
        return provider;
    }

    private static FakeMediator MediatorWithUsers(params UserOptionDto[] users)
    {
        var mediator = new FakeMediator();
        mediator.Register<GetUsersQuery, Result<IReadOnlyList<UserOptionDto>>>(_ => Result.Success<IReadOnlyList<UserOptionDto>>(users));
        return mediator;
    }

    [Fact]
    public async Task Opens_with_an_empty_autocomplete_when_no_user_is_preselected()
    {
        var mediator = MediatorWithUsers(Kovacs);

        var provider = await OpenAsync(mediator);

        Assert.DoesNotContain("Kovács János", provider.Markup);
        Assert.DoesNotContain("jelenlegi egyenlege", provider.Markup);
    }

    [Fact]
    public async Task Preselects_the_given_user_and_shows_their_balance()
    {
        var mediator = MediatorWithUsers(Kovacs, Nagy);
        mediator.Register<GetMyBalanceQuery, Result<int>>(_ => Result.Success(2200));

        var provider = await OpenAsync(mediator, preselectedUserId: Kovacs.Id);

        Assert.Contains("Kovács János jelenlegi egyenlege: 2\u00A0200 Ft", provider.Markup);
    }

    [Fact]
    public async Task Save_button_is_disabled_until_a_user_amount_and_note_are_all_set()
    {
        var mediator = MediatorWithUsers(Kovacs);

        var provider = await OpenAsync(mediator);

        var saveButton = provider.FindAll("button").First(b => b.TextContent.Contains("Mentés"));
        Assert.True(saveButton.HasAttribute("disabled"));
    }

    [Fact]
    public async Task Selecting_a_user_via_the_autocomplete_shows_their_current_balance()
    {
        var mediator = MediatorWithUsers(Kovacs);
        mediator.Register<GetMyBalanceQuery, Result<int>>(_ => Result.Success(1400));

        var provider = await OpenAsync(mediator);

        var autocomplete = provider.FindComponent<MudAutocomplete<UserOptionDto>>();
        await provider.InvokeAsync(() => autocomplete.Instance.ValueChanged.InvokeAsync(Kovacs));

        Assert.Contains("Kovács János jelenlegi egyenlege: 1\u00A0400 Ft", provider.Markup);
    }

    [Fact]
    public async Task Switching_the_preselected_user_to_someone_else_reloads_the_balance()
    {
        var mediator = MediatorWithUsers(Kovacs, Nagy);
        mediator.Register<GetMyBalanceQuery, Result<int>>(q => Result.Success(q.UserId == Kovacs.Id ? 2200 : 0));

        var provider = await OpenAsync(mediator, preselectedUserId: Kovacs.Id);
        Assert.Contains("2\u00A0200 Ft", provider.Markup);

        var autocomplete = provider.FindComponent<MudAutocomplete<UserOptionDto>>();
        await provider.InvokeAsync(() => autocomplete.Instance.ValueChanged.InvokeAsync(Nagy));

        Assert.Contains("Nagy Anna jelenlegi egyenlege: 0 Ft", provider.Markup);
    }

    [Fact]
    public async Task Saving_sends_the_command_with_the_selected_users_id_amount_and_trimmed_note()
    {
        var mediator = MediatorWithUsers(Kovacs);
        mediator.Register<GetMyBalanceQuery, Result<int>>(_ => Result.Success(0));
        AddManualCreditCommand? sentCommand = null;
        mediator.Register<AddManualCreditCommand, Result<int>>(cmd =>
        {
            sentCommand = cmd;
            return Result.Success(1);
        });

        var provider = await OpenAsync(mediator, preselectedUserId: Kovacs.Id);

        var amountField = provider.FindComponent<MudNumericField<int>>();
        await provider.InvokeAsync(() => amountField.Instance.ValueChanged.InvokeAsync(500));
        var noteField = provider.FindComponents<MudTextField<string>>().First(f => f.Instance.Label == "Indoklás");
        await provider.InvokeAsync(() => noteField.Instance.ValueChanged.InvokeAsync("  Konyhai üzemzavar  "));

        var saveButton = provider.FindAll("button").First(b => b.TextContent.Contains("Mentés"));
        await provider.InvokeAsync(() => saveButton.Click());

        Assert.NotNull(sentCommand);
        Assert.Equal(Kovacs.Id, sentCommand!.TargetUserId);
        Assert.Equal(500, sentCommand.AmountHuf);
        Assert.Equal("Konyhai üzemzavar", sentCommand.Note);
    }

    [Fact]
    public async Task Saving_uses_the_current_users_id_as_performedbyuserid()
    {
        var mediator = MediatorWithUsers(Kovacs);
        mediator.Register<GetMyBalanceQuery, Result<int>>(_ => Result.Success(0));
        AddManualCreditCommand? sentCommand = null;
        mediator.Register<AddManualCreditCommand, Result<int>>(cmd =>
        {
            sentCommand = cmd;
            return Result.Success(1);
        });

        var provider = await OpenAsync(mediator, preselectedUserId: Kovacs.Id);

        var amountField = provider.FindComponent<MudNumericField<int>>();
        await provider.InvokeAsync(() => amountField.Instance.ValueChanged.InvokeAsync(500));
        var noteField = provider.FindComponents<MudTextField<string>>().First(f => f.Instance.Label == "Indoklás");
        await provider.InvokeAsync(() => noteField.Instance.ValueChanged.InvokeAsync("Indoklás"));

        var saveButton = provider.FindAll("button").First(b => b.TextContent.Contains("Mentés"));
        await provider.InvokeAsync(() => saveButton.Click());

        Assert.Equal(1, sentCommand!.PerformedByUserId);
    }

    [Fact]
    public async Task Shows_the_error_message_returned_by_a_failed_save()
    {
        var mediator = MediatorWithUsers(Kovacs);
        mediator.Register<GetMyBalanceQuery, Result<int>>(_ => Result.Success(0));
        mediator.Register<AddManualCreditCommand, Result<int>>(
            _ => Result.Failure<int>(ErrorCodes.NotFound, "A felhasználó nem található."));

        var provider = await OpenAsync(mediator, preselectedUserId: Kovacs.Id);

        var amountField = provider.FindComponent<MudNumericField<int>>();
        await provider.InvokeAsync(() => amountField.Instance.ValueChanged.InvokeAsync(500));
        var noteField = provider.FindComponents<MudTextField<string>>().First(f => f.Instance.Label == "Indoklás");
        await provider.InvokeAsync(() => noteField.Instance.ValueChanged.InvokeAsync("Indoklás"));

        var saveButton = provider.FindAll("button").First(b => b.TextContent.Contains("Mentés"));
        await provider.InvokeAsync(() => saveButton.Click());

        Assert.Contains("A felhasználó nem található.", provider.Markup);
    }

    [Fact]
    public async Task Autocomplete_search_matches_by_department_as_well_as_name()
    {
        var other = new UserOptionDto(9, "szabo.p", 1004, "Szabó Péter", "User", "Logisztika", "Raktár");
        var mediator = MediatorWithUsers(Kovacs, other);

        var provider = await OpenAsync(mediator);

        var autocomplete = provider.FindComponent<MudAutocomplete<UserOptionDto>>();
        var searchFunc = autocomplete.Instance.SearchFunc ?? throw new InvalidOperationException("SearchFunc was not set on the autocomplete.");

        var resultsTask = searchFunc("Logisztika", CancellationToken.None) ?? throw new InvalidOperationException("SearchFunc returned a null task.");
        var results = await resultsTask;

        Assert.Equal([other], results);
    }
}
