using Bunit;
using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Components.Pages.Calendar;
using EbedrendeloApp.Features.Calendar.RemoveExcludedDay;
using EbedrendeloApp.Tests.TestSupport;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace EbedrendeloApp.Tests.Components.Calendar;

public class RevokeExclusionDialogTests : EbedrendeloApp.Tests.TestSupport.MudBunitContext
{
    public RevokeExclusionDialogTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
    }

    [Fact]
    public async Task Confirming_shows_the_restored_and_skipped_counts()
    {
        var mediator = new FakeMediator();
        mediator.Register<RemoveExcludedDayCommand, EbedrendeloApp.Common.Results.Result<RemoveExcludedDayResult>>(_ =>
            EbedrendeloApp.Common.Results.Result.Success(new RemoveExcludedDayResult(
                2, 1, [new SkippedOrderInfo("Kovács János", "erre az időszakra már készült számla, a jóváírása nem vonható vissza automatikusan")])));
        Services.AddSingleton<IMediator>(mediator);

        var provider = Render<MudDialogProvider>((Bunit.ComponentParameterCollectionBuilder<MudDialogProvider> _) => { });
        var dialogService = Services.GetRequiredService<IDialogService>();

        var parameters = new DialogParameters<RevokeExclusionDialog> { { x => x.Date, new DateOnly(2026, 8, 19) } };
        await provider.InvokeAsync(() => dialogService.ShowAsync<RevokeExclusionDialog>("Kizárás visszavonása", parameters));

        Assert.Contains("Kizárás visszavonása — 2026.08.19.", provider.Markup);

        var confirmButton = provider.FindAll("button").First(b => b.TextContent.Contains("Visszavonás megerősítése"));
        await provider.InvokeAsync(() => confirmButton.Click());

        Assert.Contains("Kovács János", provider.Markup);
        Assert.Contains("Bezárás", provider.Markup);
    }
}
