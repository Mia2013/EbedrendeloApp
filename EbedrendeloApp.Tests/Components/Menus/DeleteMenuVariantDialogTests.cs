using Bunit;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Components.Pages.Menus;
using EbedrendeloApp.Features.Menus.DeleteMenuVariant;
using EbedrendeloApp.Tests.TestSupport;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace EbedrendeloApp.Tests.Components.Menus;

public class DeleteMenuVariantDialogTests : MudBunitContext
{
    public DeleteMenuVariantDialogTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(3, "Admin Teszt", isAdmin: true));
    }

    [Fact]
    public async Task Shows_the_title_date_and_variant_code()
    {
        Services.AddSingleton<IMediator>(new FakeMediator());

        var provider = Render<MudDialogProvider>((ComponentParameterCollectionBuilder<MudDialogProvider> _) => { });
        var dialogService = Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<DeleteMenuVariantDialog> { { x => x.Date, new DateOnly(2026, 8, 20) }, { x => x.VariantCode, "B" } };

        await provider.InvokeAsync(() => dialogService.ShowAsync<DeleteMenuVariantDialog>("Variáns törlése", parameters));

        Assert.Contains("Variáns törlése", provider.Markup);
        Assert.Contains("2026.08.20. — B menü", provider.Markup);
    }

    [Fact]
    public async Task Confirming_sends_the_delete_command_with_the_current_user()
    {
        DeleteMenuVariantCommand? sentCommand = null;
        var mediator = new FakeMediator();
        mediator.Register<DeleteMenuVariantCommand, Result>(cmd =>
        {
            sentCommand = cmd;
            return Result.Success();
        });
        Services.AddSingleton<IMediator>(mediator);

        var provider = Render<MudDialogProvider>((ComponentParameterCollectionBuilder<MudDialogProvider> _) => { });
        var dialogService = Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<DeleteMenuVariantDialog> { { x => x.Date, new DateOnly(2026, 8, 20) }, { x => x.VariantCode, "B" } };
        await provider.InvokeAsync(() => dialogService.ShowAsync<DeleteMenuVariantDialog>("Variáns törlése", parameters));

        var deleteButton = provider.FindAll("button").First(b => b.TextContent.Contains("Törlés"));
        await provider.InvokeAsync(() => deleteButton.Click());

        Assert.NotNull(sentCommand);
        Assert.Equal(new DateOnly(2026, 8, 20), sentCommand!.Date);
        Assert.Equal("B", sentCommand.VariantCode);
        Assert.Equal(3, sentCommand.PerformedByUserId);
    }

    [Fact]
    public async Task Cancel_never_sends_the_delete_command()
    {
        var deleteCalled = false;
        var mediator = new FakeMediator();
        mediator.Register<DeleteMenuVariantCommand, Result>(_ => { deleteCalled = true; return Result.Success(); });
        Services.AddSingleton<IMediator>(mediator);

        var provider = Render<MudDialogProvider>((ComponentParameterCollectionBuilder<MudDialogProvider> _) => { });
        var dialogService = Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<DeleteMenuVariantDialog> { { x => x.Date, new DateOnly(2026, 8, 20) }, { x => x.VariantCode, "B" } };
        await provider.InvokeAsync(() => dialogService.ShowAsync<DeleteMenuVariantDialog>("Variáns törlése", parameters));

        var cancelButton = provider.FindAll("button").First(b => b.TextContent.Contains("Mégse"));
        await provider.InvokeAsync(() => cancelButton.Click());

        Assert.False(deleteCalled);
    }
}
