using Bunit;
using Bunit.TestDoubles;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Components.Pages.Billing;
using EbedrendeloApp.Features.Billing.AddManualCredit;
using EbedrendeloApp.Features.Billing.GetBalances;
using EbedrendeloApp.Features.Billing.GetMyBalance;
using EbedrendeloApp.Features.Users.GetUsers;
using EbedrendeloApp.Tests.TestSupport;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace EbedrendeloApp.Tests.Components.Billing;

public class AdminBalancesTests : MudBunitContext
{
    public AdminBalancesTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private static UserBalanceDto Balance(int userId, string name, int huf, string? igazgatosag = null, string? osztaly = null) =>
        new(userId, name, igazgatosag, osztaly, huf);

    [Fact]
    public void Redirects_non_admin_users_to_the_today_menu_page()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(2, "Dolgozó Teszt", isAdmin: false));
        Services.AddSingleton<IMediator>(new FakeMediator());

        Render<AdminBalances>();

        var navigationManager = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/mai-menu", navigationManager.Uri);
    }

    [Fact]
    public void Lists_balances_returned_by_the_query()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        var mediator = new FakeMediator();
        mediator.Register<GetBalancesQuery, Result<IReadOnlyList<UserBalanceDto>>>(
            _ => Result.Success<IReadOnlyList<UserBalanceDto>>([Balance(2, "Kovács János", 2200, "Gyártás", "1. üzem")]));
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<AdminBalances>();

        Assert.Contains("Kovács János", cut.Markup);
        Assert.Contains("Gyártás — 1. üzem", cut.Markup);
        Assert.Contains("2\u00A0200 Ft", cut.Markup);
    }

    [Fact]
    public void Hides_the_department_separator_when_only_one_field_is_present()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        var mediator = new FakeMediator();
        mediator.Register<GetBalancesQuery, Result<IReadOnlyList<UserBalanceDto>>>(
            _ => Result.Success<IReadOnlyList<UserBalanceDto>>([Balance(2, "Kovács János", 2200, igazgatosag: "Gyártás", osztaly: null)]));
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<AdminBalances>();

        Assert.DoesNotContain("Gyártás —", cut.Markup);
        Assert.Contains("Gyártás", cut.Markup);
    }

    [Fact]
    public async Task Filters_rows_by_search_text_matching_name()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        var mediator = new FakeMediator();
        mediator.Register<GetBalancesQuery, Result<IReadOnlyList<UserBalanceDto>>>(_ => Result.Success<IReadOnlyList<UserBalanceDto>>(
        [
            Balance(2, "Kovács János", 2200),
            Balance(3, "Tóth Eszter", 1400),
        ]));
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<AdminBalances>();
        var search = cut.FindComponent<MudTextField<string>>();
        await cut.InvokeAsync(() => search.Instance.ValueChanged.InvokeAsync("Tóth"));

        Assert.DoesNotContain("Kovács János", cut.Markup);
        Assert.Contains("Tóth Eszter", cut.Markup);
    }

    [Fact]
    public async Task Filters_rows_by_search_text_matching_department()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        var mediator = new FakeMediator();
        mediator.Register<GetBalancesQuery, Result<IReadOnlyList<UserBalanceDto>>>(_ => Result.Success<IReadOnlyList<UserBalanceDto>>(
        [
            Balance(2, "Kovács János", 2200, igazgatosag: "Gyártás"),
            Balance(3, "Tóth Eszter", 1400, igazgatosag: "Logisztika"),
        ]));
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<AdminBalances>();
        var search = cut.FindComponent<MudTextField<string>>();
        await cut.InvokeAsync(() => search.Instance.ValueChanged.InvokeAsync("Logisztika"));

        Assert.DoesNotContain("Kovács János", cut.Markup);
        Assert.Contains("Tóth Eszter", cut.Markup);
    }

    [Fact]
    public void Shows_empty_state_when_there_are_no_nonzero_balances()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        var mediator = new FakeMediator();
        mediator.Register<GetBalancesQuery, Result<IReadOnlyList<UserBalanceDto>>>(_ => Result.Success<IReadOnlyList<UserBalanceDto>>([]));
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<AdminBalances>();

        Assert.Contains("Nincs megjeleníthető (nem nulla) egyenleg.", cut.Markup);
    }

    [Fact]
    public async Task Opening_the_page_level_button_opens_the_dialog_without_a_preselected_user()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        var mediator = new FakeMediator();
        mediator.Register<GetBalancesQuery, Result<IReadOnlyList<UserBalanceDto>>>(_ => Result.Success<IReadOnlyList<UserBalanceDto>>([]));
        mediator.Register<GetUsersQuery, Result<IReadOnlyList<UserOptionDto>>>(_ => Result.Success<IReadOnlyList<UserOptionDto>>([]));
        Services.AddSingleton<IMediator>(mediator);

        var dialogProvider = Render<MudDialogProvider>();
        var cut = Render<AdminBalances>();

        var addButton = cut.FindAll("button").First(b => b.TextContent.Contains("Új jóváírás"));
        await cut.InvokeAsync(() => addButton.Click());

        var dialog = dialogProvider.FindComponent<ManualCreditDialog>();
        Assert.Null(dialog.Instance.PreselectedUserId);
    }

    [Fact]
    public async Task Opening_the_row_button_opens_the_dialog_with_that_rows_user_preselected()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        var mediator = new FakeMediator();
        mediator.Register<GetBalancesQuery, Result<IReadOnlyList<UserBalanceDto>>>(
            _ => Result.Success<IReadOnlyList<UserBalanceDto>>([Balance(7, "Kovács János", 2200)]));
        mediator.Register<GetUsersQuery, Result<IReadOnlyList<UserOptionDto>>>(_ => Result.Success<IReadOnlyList<UserOptionDto>>([]));
        Services.AddSingleton<IMediator>(mediator);

        var dialogProvider = Render<MudDialogProvider>();
        var cut = Render<AdminBalances>();

        var rowButton = cut.Find("button[title='Jóváírás hozzáadása']");
        await cut.InvokeAsync(() => rowButton.Click());

        var dialog = dialogProvider.FindComponent<ManualCreditDialog>();
        Assert.Equal(7, dialog.Instance.PreselectedUserId);
    }

    [Fact]
    public async Task Reloads_the_list_after_a_successful_manual_credit()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        var mediator = new FakeMediator();
        var loadCount = 0;
        mediator.Register<GetBalancesQuery, Result<IReadOnlyList<UserBalanceDto>>>(_ =>
        {
            loadCount++;
            return Result.Success<IReadOnlyList<UserBalanceDto>>([]);
        });
        mediator.Register<GetUsersQuery, Result<IReadOnlyList<UserOptionDto>>>(
            _ => Result.Success<IReadOnlyList<UserOptionDto>>([new UserOptionDto(9, "u9", 9, "Kovács János", "User", null, null)]));
        mediator.Register<GetMyBalanceQuery, Result<int>>(_ => Result.Success(0));
        mediator.Register<AddManualCreditCommand, Result<int>>(_ => Result.Success(1));
        Services.AddSingleton<IMediator>(mediator);

        var dialogProvider = Render<MudDialogProvider>();
        var cut = Render<AdminBalances>();
        Assert.Equal(1, loadCount);

        var addButton = cut.FindAll("button").First(b => b.TextContent.Contains("Új jóváírás"));
        await cut.InvokeAsync(() => addButton.Click());

        var nameAutocomplete = dialogProvider.FindComponent<MudAutocomplete<UserOptionDto>>();
        await dialogProvider.InvokeAsync(() => nameAutocomplete.Instance.ValueChanged.InvokeAsync(new UserOptionDto(9, "u9", 9, "Kovács János", "User", null, null)));

        var amountField = dialogProvider.FindComponent<MudNumericField<int>>();
        await dialogProvider.InvokeAsync(() => amountField.Instance.ValueChanged.InvokeAsync(500));

        var noteField = dialogProvider.FindComponents<MudTextField<string>>().First(f => f.Instance.Label == "Indoklás");
        await dialogProvider.InvokeAsync(() => noteField.Instance.ValueChanged.InvokeAsync("Indoklás"));

        var saveButton = dialogProvider.FindAll("button").First(b => b.TextContent.Contains("Mentés"));
        await dialogProvider.InvokeAsync(() => saveButton.Click());

        Assert.Equal(2, loadCount);
    }
}
