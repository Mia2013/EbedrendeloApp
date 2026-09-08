using Bunit;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Components.Pages.Billing;
using EbedrendeloApp.Features.Billing.GeneratePeriodInvoices;
using EbedrendeloApp.Tests.TestSupport;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace EbedrendeloApp.Tests.Components.Billing;

public class GenerateInvoicesDialogTests : MudBunitContext
{
    public GenerateInvoicesDialogTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(7, "Admin Teszt", isAdmin: true));
    }

    [Fact]
    public async Task Shows_the_period_name()
    {
        Services.AddSingleton<IMediator>(new FakeMediator());

        var provider = Render<MudDialogProvider>((ComponentParameterCollectionBuilder<MudDialogProvider> _) => { });
        var dialogService = Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<GenerateInvoicesDialog> { { x => x.PeriodId, 3 }, { x => x.PeriodName, "2026. szeptember" } };

        await provider.InvokeAsync(() => dialogService.ShowAsync<GenerateInvoicesDialog>("Számlák generálása", parameters));

        Assert.Contains("2026. szeptember", provider.Markup);
    }

    [Fact]
    public async Task Confirming_sends_the_generate_command_with_the_period_and_current_user()
    {
        GeneratePeriodInvoicesCommand? sentCommand = null;
        var mediator = new FakeMediator();
        mediator.Register<GeneratePeriodInvoicesCommand, Result<BatchInvoiceResult>>(cmd =>
        {
            sentCommand = cmd;
            return Result.Success(new BatchInvoiceResult([], []));
        });
        Services.AddSingleton<IMediator>(mediator);

        var provider = Render<MudDialogProvider>((ComponentParameterCollectionBuilder<MudDialogProvider> _) => { });
        var dialogService = Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<GenerateInvoicesDialog> { { x => x.PeriodId, 3 }, { x => x.PeriodName, "2026. szeptember" } };
        await provider.InvokeAsync(() => dialogService.ShowAsync<GenerateInvoicesDialog>("Számlák generálása", parameters));

        var confirmButton = provider.FindAll("button").First(b => b.TextContent.Contains("Generálás"));
        await provider.InvokeAsync(() => confirmButton.Click());

        Assert.NotNull(sentCommand);
        Assert.Equal(3, sentCommand!.OrderingPeriodId);
        Assert.Equal(7, sentCommand.GeneratedByUserId);
    }

    [Fact]
    public async Task Shows_the_servers_error_message_on_failure()
    {
        var mediator = new FakeMediator();
        mediator.Register<GeneratePeriodInvoicesCommand, Result<BatchInvoiceResult>>(
            _ => Result.Failure<BatchInvoiceResult>(ErrorCodes.OrderWindowOpen, "A rendelési határidő még nem telt le."));
        Services.AddSingleton<IMediator>(mediator);

        var provider = Render<MudDialogProvider>((ComponentParameterCollectionBuilder<MudDialogProvider> _) => { });
        var dialogService = Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<GenerateInvoicesDialog> { { x => x.PeriodId, 3 }, { x => x.PeriodName, "2026. szeptember" } };
        await provider.InvokeAsync(() => dialogService.ShowAsync<GenerateInvoicesDialog>("Számlák generálása", parameters));

        var confirmButton = provider.FindAll("button").First(b => b.TextContent.Contains("Generálás"));
        await provider.InvokeAsync(() => confirmButton.Click());

        Assert.Contains("A rendelési határidő még nem telt le.", provider.Markup);
    }

    [Fact]
    public async Task Cancel_never_sends_the_generate_command()
    {
        var called = false;
        var mediator = new FakeMediator();
        mediator.Register<GeneratePeriodInvoicesCommand, Result<BatchInvoiceResult>>(_ =>
        {
            called = true;
            return Result.Success(new BatchInvoiceResult([], []));
        });
        Services.AddSingleton<IMediator>(mediator);

        var provider = Render<MudDialogProvider>((ComponentParameterCollectionBuilder<MudDialogProvider> _) => { });
        var dialogService = Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<GenerateInvoicesDialog> { { x => x.PeriodId, 3 }, { x => x.PeriodName, "2026. szeptember" } };
        await provider.InvokeAsync(() => dialogService.ShowAsync<GenerateInvoicesDialog>("Számlák generálása", parameters));

        var cancelButton = provider.FindAll("button").First(b => b.TextContent.Contains("Mégse"));
        await provider.InvokeAsync(() => cancelButton.Click());

        Assert.False(called);
    }
}
