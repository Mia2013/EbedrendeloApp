using Bunit;
using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Components.Pages.Calendar;
using EbedrendeloApp.Features.Calendar.ExcludeDay;
using EbedrendeloApp.Features.Calendar.GetExcludedDays;
using EbedrendeloApp.Tests.TestSupport;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace EbedrendeloApp.Tests.Components.Calendar;

public class ExcludeDayDialogTests : EbedrendeloApp.Tests.TestSupport.MudBunitContext
{
    public ExcludeDayDialogTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));

        var mediator = new FakeMediator();
        mediator.Register<GetExcludedDaysQuery, IReadOnlyList<ExcludedDayDto>>(_ => []);
        Services.AddSingleton<IMediator>(mediator);
    }

    [Fact]
    public async Task Shows_the_title_and_the_cancellation_warning()
    {
        var provider = Render<MudDialogProvider>((Bunit.ComponentParameterCollectionBuilder<MudDialogProvider> _) => { });
        var dialogService = Services.GetRequiredService<IDialogService>();

        await provider.InvokeAsync(() => dialogService.ShowAsync<ExcludeDayDialog>("Nap kizárása"));

        Assert.Contains("Nap kizárása", provider.Markup);
        Assert.Contains("Csak jövőbeli nap zárható ki.", provider.Markup);
        Assert.Contains("teljes összegű jóváírást és értesítést kapnak", provider.Markup);
    }

    [Fact]
    public async Task Submits_freely_typed_reasons_not_only_ones_picked_from_the_suggestion_list()
    {
        ExcludeDayCommand? sentCommand = null;
        var mediator = (FakeMediator)Services.GetRequiredService<IMediator>();
        mediator.Register<ExcludeDayCommand, EbedrendeloApp.Common.Results.Result>(cmd =>
        {
            sentCommand = cmd;
            return EbedrendeloApp.Common.Results.Result.Success();
        });

        var provider = Render<MudDialogProvider>((Bunit.ComponentParameterCollectionBuilder<MudDialogProvider> _) => { });
        var dialogService = Services.GetRequiredService<IDialogService>();
        await provider.InvokeAsync(() => dialogService.ShowAsync<ExcludeDayDialog>("Nap kizárása"));

        var reasonLabel = provider.FindAll("label").First(l => l.TextContent == "Indoklás");
        var reasonInput = provider.Find($"#{reasonLabel.GetAttribute("for")}");

        await provider.InvokeAsync(() => reasonInput.Input("Egyedi, listán nem szereplő indok"));
        await provider.InvokeAsync(() => reasonInput.TriggerEvent("onblur", new Microsoft.AspNetCore.Components.Web.FocusEventArgs()));

        var submitButton = provider.FindAll("button").First(b => b.TextContent.Contains("Nap kizárása"));
        await provider.InvokeAsync(() => submitButton.Click());

        Assert.NotNull(sentCommand);
        Assert.Equal("Egyedi, listán nem szereplő indok", sentCommand!.Reason);
    }
}
