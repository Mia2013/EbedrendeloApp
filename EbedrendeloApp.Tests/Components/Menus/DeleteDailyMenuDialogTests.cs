using Bunit;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Components.Pages.Menus;
using EbedrendeloApp.Features.Menus.DeleteDailyMenu;
using EbedrendeloApp.Tests.TestSupport;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace EbedrendeloApp.Tests.Components.Menus;

public class DeleteDailyMenuDialogTests : MudBunitContext
{
    public DeleteDailyMenuDialogTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(4, "Admin Teszt", isAdmin: true));
    }

    [Fact]
    public async Task Confirming_sends_the_delete_command_and_closes_successfully()
    {
        DeleteDailyMenuCommand? sentCommand = null;
        var mediator = new FakeMediator();
        mediator.Register<DeleteDailyMenuCommand, Result>(cmd =>
        {
            sentCommand = cmd;
            return Result.Success();
        });
        Services.AddSingleton<IMediator>(mediator);

        var provider = Render<MudDialogProvider>((ComponentParameterCollectionBuilder<MudDialogProvider> _) => { });
        var dialogService = Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<DeleteDailyMenuDialog> { { x => x.Date, new DateOnly(2026, 8, 20) } };
        await provider.InvokeAsync(() => dialogService.ShowAsync<DeleteDailyMenuDialog>("Nap menüjének törlése", parameters));

        var deleteButton = provider.FindAll("button").First(b => b.TextContent.Contains("Törlés"));
        await provider.InvokeAsync(() => deleteButton.Click());

        Assert.NotNull(sentCommand);
        Assert.Equal(new DateOnly(2026, 8, 20), sentCommand!.Date);
        Assert.Equal(4, sentCommand.PerformedByUserId);
    }

    [Fact]
    public async Task Shows_the_servers_error_message_without_closing_when_the_command_fails()
    {
        var mediator = new FakeMediator();
        mediator.Register<DeleteDailyMenuCommand, Result>(_ => Result.Failure(EbedrendeloApp.Common.Results.ErrorCodes.DayClosed, "A nap már le van zárva."));
        Services.AddSingleton<IMediator>(mediator);

        var provider = Render<MudDialogProvider>((ComponentParameterCollectionBuilder<MudDialogProvider> _) => { });
        var dialogService = Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<DeleteDailyMenuDialog> { { x => x.Date, new DateOnly(2026, 8, 20) } };
        await provider.InvokeAsync(() => dialogService.ShowAsync<DeleteDailyMenuDialog>("Nap menüjének törlése", parameters));

        var deleteButton = provider.FindAll("button").First(b => b.TextContent.Contains("Törlés"));
        await provider.InvokeAsync(() => deleteButton.Click());

        Assert.Contains("A nap már le van zárva.", provider.Markup);
    }
}
