using Bunit;
using EbedrendeloApp.Components.Shared;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace EbedrendeloApp.Tests.Components.Shared;

public class ConfirmDialogTests : MudBunitContext
{
    public ConfirmDialogTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task Shows_the_title_message_and_custom_confirm_text()
    {
        var provider = Render<MudDialogProvider>();
        var dialogService = Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<ConfirmDialog>
        {
            { x => x.Title, "Leves visszavonása" },
            { x => x.Message, "Biztosan visszavonod a mai leves-ajánlatot?" },
            { x => x.ConfirmText, "Visszavonás" },
        };

        await provider.InvokeAsync(() => dialogService.ShowAsync<ConfirmDialog>("Leves visszavonása", parameters));

        Assert.Contains("Leves visszavonása", provider.Markup);
        Assert.Contains("Biztosan visszavonod a mai leves-ajánlatot?", provider.Markup);
        Assert.Contains("Visszavonás", provider.Markup);
    }

    [Fact]
    public async Task Confirm_button_closes_with_a_non_canceled_result()
    {
        var provider = Render<MudDialogProvider>();
        var dialogService = Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<ConfirmDialog> { { x => x.Message, "Biztosan?" } };

        IDialogReference dialogRef = null!;
        await provider.InvokeAsync(async () => dialogRef = await dialogService.ShowAsync<ConfirmDialog>("Megerősítés", parameters));

        var confirmButton = provider.FindAll("button").First(b => b.TextContent.Contains("Törlés"));
        await provider.InvokeAsync(() => confirmButton.Click());

        var result = await dialogRef.Result;
        Assert.NotNull(result);
        Assert.False(result!.Canceled);
    }

    [Fact]
    public async Task Cancel_button_closes_as_canceled()
    {
        var provider = Render<MudDialogProvider>();
        var dialogService = Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<ConfirmDialog> { { x => x.Message, "Biztosan?" } };

        IDialogReference dialogRef = null!;
        await provider.InvokeAsync(async () => dialogRef = await dialogService.ShowAsync<ConfirmDialog>("Megerősítés", parameters));

        var cancelButton = provider.FindAll("button").First(b => b.TextContent.Contains("Mégse"));
        await provider.InvokeAsync(() => cancelButton.Click());

        var result = await dialogRef.Result;
        Assert.NotNull(result);
        Assert.True(result!.Canceled);
    }
}
