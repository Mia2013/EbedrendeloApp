using Bunit;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Components.Pages.Billing;
using EbedrendeloApp.Features.Billing.GetInvoices;
using EbedrendeloApp.Features.Billing.MarkInvoicePaid;
using EbedrendeloApp.Tests.TestSupport;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace EbedrendeloApp.Tests.Components.Billing;

public class MarkInvoicePaidDialogTests : MudBunitContext
{
    private static readonly InvoiceDto Invoice = new(
        Id: 10, UserId: 5, UserDisplayName: "Nagy Béla", OrderingPeriodId: 1, PeriodName: "2026. szeptember",
        SequenceNumber: 1, DayCount: 20, GrossHuf: 28000, CreditAppliedHuf: 3000, PayableHuf: 25000,
        IsPaid: false, PaidAtUtc: null, GeneratedAtUtc: new DateTime(2026, 9, 16, 9, 0, 0));

    public MarkInvoicePaidDialogTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(7, "Admin Teszt", isAdmin: true));
    }

    [Fact]
    public async Task Shows_the_employee_period_and_amount()
    {
        Services.AddSingleton<IMediator>(new FakeMediator());

        var provider = Render<MudDialogProvider>((ComponentParameterCollectionBuilder<MudDialogProvider> _) => { });
        var dialogService = Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<MarkInvoicePaidDialog> { { x => x.Invoice, Invoice } };

        await provider.InvokeAsync(() => dialogService.ShowAsync<MarkInvoicePaidDialog>("Számla fizetettnek jelölése", parameters));

        Assert.Contains("Nagy Béla", provider.Markup);
        Assert.Contains("2026. szeptember", provider.Markup);
        Assert.Contains("25 000 Ft", provider.Markup);
    }

    [Fact]
    public async Task Confirming_sends_the_mark_paid_command_with_the_current_user()
    {
        MarkInvoicePaidCommand? sentCommand = null;
        var mediator = new FakeMediator();
        mediator.Register<MarkInvoicePaidCommand, Result>(cmd =>
        {
            sentCommand = cmd;
            return Result.Success();
        });
        Services.AddSingleton<IMediator>(mediator);

        var provider = Render<MudDialogProvider>((ComponentParameterCollectionBuilder<MudDialogProvider> _) => { });
        var dialogService = Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<MarkInvoicePaidDialog> { { x => x.Invoice, Invoice } };
        await provider.InvokeAsync(() => dialogService.ShowAsync<MarkInvoicePaidDialog>("Számla fizetettnek jelölése", parameters));

        var confirmButton = provider.FindAll("button").First(b => b.TextContent.Contains("Fizetettnek jelölés"));
        await provider.InvokeAsync(() => confirmButton.Click());

        Assert.NotNull(sentCommand);
        Assert.Equal(Invoice.Id, sentCommand!.InvoiceId);
        Assert.Equal(7, sentCommand.MarkedByUserId);
    }

    [Fact]
    public async Task Shows_the_servers_error_message_on_failure()
    {
        var mediator = new FakeMediator();
        mediator.Register<MarkInvoicePaidCommand, Result>(_ => Result.Failure(ErrorCodes.AlreadyPaid, "A számla már ki van fizetve jelölve."));
        Services.AddSingleton<IMediator>(mediator);

        var provider = Render<MudDialogProvider>((ComponentParameterCollectionBuilder<MudDialogProvider> _) => { });
        var dialogService = Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<MarkInvoicePaidDialog> { { x => x.Invoice, Invoice } };
        await provider.InvokeAsync(() => dialogService.ShowAsync<MarkInvoicePaidDialog>("Számla fizetettnek jelölése", parameters));

        var confirmButton = provider.FindAll("button").First(b => b.TextContent.Contains("Fizetettnek jelölés"));
        await provider.InvokeAsync(() => confirmButton.Click());

        Assert.Contains("A számla már ki van fizetve jelölve.", provider.Markup);
    }

    [Fact]
    public async Task Cancel_never_sends_the_mark_paid_command()
    {
        var called = false;
        var mediator = new FakeMediator();
        mediator.Register<MarkInvoicePaidCommand, Result>(_ =>
        {
            called = true;
            return Result.Success();
        });
        Services.AddSingleton<IMediator>(mediator);

        var provider = Render<MudDialogProvider>((ComponentParameterCollectionBuilder<MudDialogProvider> _) => { });
        var dialogService = Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<MarkInvoicePaidDialog> { { x => x.Invoice, Invoice } };
        await provider.InvokeAsync(() => dialogService.ShowAsync<MarkInvoicePaidDialog>("Számla fizetettnek jelölése", parameters));

        var cancelButton = provider.FindAll("button").First(b => b.TextContent.Contains("Mégse"));
        await provider.InvokeAsync(() => cancelButton.Click());

        Assert.False(called);
    }
}
