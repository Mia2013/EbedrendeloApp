using Bunit;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Components.Pages.Orders;
using EbedrendeloApp.Features.Orders;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace EbedrendeloApp.Tests.Components.Orders;

public class PlaceOrderResultDialogTests : MudBunitContext
{
    public PlaceOrderResultDialogTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task Shows_the_succeeded_and_skipped_counts_with_the_skip_reasons()
    {
        var result = new BatchOrderResult(
            [new DayResult(new DateOnly(2026, 8, 17), "A"), new DayResult(new DateOnly(2026, 8, 18), "B")],
            [new DaySkip(new DateOnly(2026, 8, 20), ErrorCodes.DayExcluded)]);

        var provider = Render<MudDialogProvider>();
        var dialogService = Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<PlaceOrderResultDialog> { { x => x.Result, result } };
        await provider.InvokeAsync(() => dialogService.ShowAsync<PlaceOrderResultDialog>("Rendelés eredménye", parameters));

        Assert.Contains("2", provider.Markup);
        Assert.Contains("Sikeres", provider.Markup);
        Assert.Contains("1", provider.Markup);
        Assert.Contains("Kihagyva", provider.Markup);
        Assert.Contains("2026.08.20.", provider.Markup);
        Assert.Contains("Kizárt nap", provider.Markup);
    }

    [Fact]
    public async Task Hides_the_skipped_card_when_nothing_was_skipped()
    {
        var result = new BatchOrderResult([new DayResult(new DateOnly(2026, 8, 17), "A")], []);

        var provider = Render<MudDialogProvider>();
        var dialogService = Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<PlaceOrderResultDialog> { { x => x.Result, result } };
        await provider.InvokeAsync(() => dialogService.ShowAsync<PlaceOrderResultDialog>("Rendelés eredménye", parameters));

        Assert.Contains("Sikeres", provider.Markup);
        Assert.DoesNotContain("Kihagyva", provider.Markup);
    }

    [Fact]
    public async Task Shows_a_custom_title_when_reused_for_a_different_batch_operation()
    {
        // The cancellation flow (UserCalendar.razor) reuses this dialog with its own Title/Icon/IconColor
        // instead of the order-placement defaults.
        var result = new BatchOrderResult([new DayResult(new DateOnly(2026, 8, 17), "A")], []);

        var provider = Render<MudDialogProvider>();
        var dialogService = Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<PlaceOrderResultDialog>
        {
            { x => x.Result, result },
            { x => x.Title, "Lemondás eredménye" },
        };
        await provider.InvokeAsync(() => dialogService.ShowAsync<PlaceOrderResultDialog>("Lemondás eredménye", parameters));

        Assert.Contains("Lemondás eredménye", provider.Markup);
        Assert.DoesNotContain("Rendelés eredménye", provider.Markup);
    }
}
