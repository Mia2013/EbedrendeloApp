using Bunit;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Components.Pages.Kitchen;
using EbedrendeloApp.Features.Kitchen;
using EbedrendeloApp.Features.Kitchen.CloseDay;
using EbedrendeloApp.Tests.TestSupport;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace EbedrendeloApp.Tests.Components.Kitchen;

public class CloseDayDialogTests : MudBunitContext
{
    private static readonly DateOnly Date = new(2026, 9, 11);
    private static readonly KitchenSummaryDto Summary = new(
        Date, IsClosed: false,
        [new KitchenVariantLineDto("A", "Gulyásleves + Rántott hús", 18), new KitchenVariantLineDto("B", "Gulyásleves + Halászlé", 11)],
        TotalPortions: 29);

    public CloseDayDialogTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(7, "Admin Teszt", isAdmin: true));
    }

    [Fact]
    public async Task Shows_the_date_and_the_variant_breakdown()
    {
        Services.AddSingleton<IMediator>(new FakeMediator());

        var provider = Render<MudDialogProvider>((ComponentParameterCollectionBuilder<MudDialogProvider> _) => { });
        var dialogService = Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<CloseDayDialog> { { x => x.Date, Date }, { x => x.Summary, Summary } };

        await provider.InvokeAsync(() => dialogService.ShowAsync<CloseDayDialog>("Nap lezárása", parameters));

        Assert.Contains("2026.09.11.", provider.Markup);
        Assert.Contains("29", provider.Markup);
        Assert.Contains("A: 18", provider.Markup);
        Assert.Contains("B: 11", provider.Markup);
    }

    [Fact]
    public async Task Confirming_sends_the_close_command_with_the_current_user()
    {
        CloseDayCommand? sentCommand = null;
        var mediator = new FakeMediator();
        mediator.Register<CloseDayCommand, Result<KitchenClosureDto>>(cmd =>
        {
            sentCommand = cmd;
            return Result.Success(new KitchenClosureDto(Date, DateTime.UtcNow, 7, "Admin Teszt", 29, [], null, null, null));
        });
        Services.AddSingleton<IMediator>(mediator);

        var provider = Render<MudDialogProvider>((ComponentParameterCollectionBuilder<MudDialogProvider> _) => { });
        var dialogService = Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<CloseDayDialog> { { x => x.Date, Date }, { x => x.Summary, Summary } };
        await provider.InvokeAsync(() => dialogService.ShowAsync<CloseDayDialog>("Nap lezárása", parameters));

        var closeButton = provider.FindAll("button").First(b => b.TextContent.Contains("Lezárás"));
        await provider.InvokeAsync(() => closeButton.Click());

        Assert.NotNull(sentCommand);
        Assert.Equal(Date, sentCommand!.Date);
        Assert.Equal(7, sentCommand.ClosedByUserId);
    }

    [Fact]
    public async Task Shows_the_servers_error_message_on_failure()
    {
        var mediator = new FakeMediator();
        mediator.Register<CloseDayCommand, Result<KitchenClosureDto>>(_ => Result.Failure<KitchenClosureDto>(ErrorCodes.DayClosed, "A nap már le van zárva."));
        Services.AddSingleton<IMediator>(mediator);

        var provider = Render<MudDialogProvider>((ComponentParameterCollectionBuilder<MudDialogProvider> _) => { });
        var dialogService = Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<CloseDayDialog> { { x => x.Date, Date }, { x => x.Summary, Summary } };
        await provider.InvokeAsync(() => dialogService.ShowAsync<CloseDayDialog>("Nap lezárása", parameters));

        var closeButton = provider.FindAll("button").First(b => b.TextContent.Contains("Lezárás"));
        await provider.InvokeAsync(() => closeButton.Click());

        Assert.Contains("A nap már le van zárva.", provider.Markup);
    }

    [Fact]
    public async Task Cancel_never_sends_the_close_command()
    {
        var closeCalled = false;
        var mediator = new FakeMediator();
        mediator.Register<CloseDayCommand, Result<KitchenClosureDto>>(_ =>
        {
            closeCalled = true;
            return Result.Success(new KitchenClosureDto(Date, DateTime.UtcNow, 7, "Admin Teszt", 29, [], null, null, null));
        });
        Services.AddSingleton<IMediator>(mediator);

        var provider = Render<MudDialogProvider>((ComponentParameterCollectionBuilder<MudDialogProvider> _) => { });
        var dialogService = Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<CloseDayDialog> { { x => x.Date, Date }, { x => x.Summary, Summary } };
        await provider.InvokeAsync(() => dialogService.ShowAsync<CloseDayDialog>("Nap lezárása", parameters));

        var cancelButton = provider.FindAll("button").First(b => b.TextContent.Contains("Mégse"));
        await provider.InvokeAsync(() => cancelButton.Click());

        Assert.False(closeCalled);
    }
}
