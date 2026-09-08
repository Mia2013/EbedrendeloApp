using Bunit;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Components.Pages.Kitchen;
using EbedrendeloApp.Features.Kitchen;
using EbedrendeloApp.Features.Kitchen.ReopenDay;
using EbedrendeloApp.Tests.TestSupport;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace EbedrendeloApp.Tests.Components.Kitchen;

public class ReopenDayDialogTests : MudBunitContext
{
    private static readonly DateOnly Date = new(2026, 9, 9);
    private static readonly KitchenClosureDto Closure = new(
        Date, new DateTime(2026, 9, 9, 11, 15, 0, DateTimeKind.Utc), 7, "Nagy Anna",
        28, [new KitchenClosureLineDto("A", "Gulyásleves", 28)], null, null, null);

    public ReopenDayDialogTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(9, "Kovács János", isAdmin: true));
    }

    [Fact]
    public async Task Shows_the_date_and_the_preserved_total()
    {
        Services.AddSingleton<IMediator>(new FakeMediator());

        var provider = Render<MudDialogProvider>((ComponentParameterCollectionBuilder<MudDialogProvider> _) => { });
        var dialogService = Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<ReopenDayDialog> { { x => x.Date, Date }, { x => x.Closure, Closure } };

        await provider.InvokeAsync(() => dialogService.ShowAsync<ReopenDayDialog>("Nap újranyitása", parameters));

        Assert.Contains("2026.09.09.", provider.Markup);
        Assert.Contains("28", provider.Markup);
    }

    [Fact]
    public async Task Confirming_sends_the_reopen_command_with_the_current_user()
    {
        ReopenDayCommand? sentCommand = null;
        var mediator = new FakeMediator();
        mediator.Register<ReopenDayCommand, Result>(cmd =>
        {
            sentCommand = cmd;
            return Result.Success();
        });
        Services.AddSingleton<IMediator>(mediator);

        var provider = Render<MudDialogProvider>((ComponentParameterCollectionBuilder<MudDialogProvider> _) => { });
        var dialogService = Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<ReopenDayDialog> { { x => x.Date, Date }, { x => x.Closure, Closure } };
        await provider.InvokeAsync(() => dialogService.ShowAsync<ReopenDayDialog>("Nap újranyitása", parameters));

        var reopenButton = provider.FindAll("button").First(b => b.TextContent.Contains("Újranyitás"));
        await provider.InvokeAsync(() => reopenButton.Click());

        Assert.NotNull(sentCommand);
        Assert.Equal(Date, sentCommand!.Date);
        Assert.Equal(9, sentCommand.ReopenedByUserId);
    }

    [Fact]
    public async Task Shows_the_servers_error_message_on_failure()
    {
        var mediator = new FakeMediator();
        mediator.Register<ReopenDayCommand, Result>(_ => Result.Failure(ErrorCodes.NotFound, "A napra nincs érvényben lévő zárás."));
        Services.AddSingleton<IMediator>(mediator);

        var provider = Render<MudDialogProvider>((ComponentParameterCollectionBuilder<MudDialogProvider> _) => { });
        var dialogService = Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<ReopenDayDialog> { { x => x.Date, Date }, { x => x.Closure, Closure } };
        await provider.InvokeAsync(() => dialogService.ShowAsync<ReopenDayDialog>("Nap újranyitása", parameters));

        var reopenButton = provider.FindAll("button").First(b => b.TextContent.Contains("Újranyitás"));
        await provider.InvokeAsync(() => reopenButton.Click());

        Assert.Contains("A napra nincs érvényben lévő zárás.", provider.Markup);
    }

    [Fact]
    public async Task Cancel_never_sends_the_reopen_command()
    {
        var reopenCalled = false;
        var mediator = new FakeMediator();
        mediator.Register<ReopenDayCommand, Result>(_ =>
        {
            reopenCalled = true;
            return Result.Success();
        });
        Services.AddSingleton<IMediator>(mediator);

        var provider = Render<MudDialogProvider>((ComponentParameterCollectionBuilder<MudDialogProvider> _) => { });
        var dialogService = Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<ReopenDayDialog> { { x => x.Date, Date }, { x => x.Closure, Closure } };
        await provider.InvokeAsync(() => dialogService.ShowAsync<ReopenDayDialog>("Nap újranyitása", parameters));

        var cancelButton = provider.FindAll("button").First(b => b.TextContent.Contains("Mégse"));
        await provider.InvokeAsync(() => cancelButton.Click());

        Assert.False(reopenCalled);
    }
}
