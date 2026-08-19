using Bunit;
using EbedrendeloApp.Components.Pages.Calendar;
using EbedrendeloApp.Features.Calendar;
using EbedrendeloApp.Features.Calendar.UpsertOrderingPeriod;
using EbedrendeloApp.Tests.TestSupport;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace EbedrendeloApp.Tests.Components.Calendar;

public class OrderingPeriodDialogTests : EbedrendeloApp.Tests.TestSupport.MudBunitContext
{
    public OrderingPeriodDialogTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task Shows_the_create_title_and_the_locked_state_hint_for_a_period_with_orders()
    {
        Services.AddSingleton<IMediator>(new FakeMediator());

        var provider = Render<MudDialogProvider>((Bunit.ComponentParameterCollectionBuilder<MudDialogProvider> _) => { });
        var dialogService = Services.GetRequiredService<IDialogService>();

        var existing = new OrderingPeriodDto(1, "2026. augusztus", new DateOnly(2026, 8, 5), new DateOnly(2026, 9, 5), new DateTime(2026, 7, 26, 10, 0, 0), true, HasOrders: true);
        var parameters = new DialogParameters<OrderingPeriodDialog> { { x => x.Existing, existing } };

        await provider.InvokeAsync(() => dialogService.ShowAsync<OrderingPeriodDialog>("Rendelési időszak szerkesztése", parameters));

        Assert.Contains("Rendelési időszak szerkesztése", provider.Markup);
        Assert.Contains("csak a név és a nyitva tartás módosítható", provider.Markup);
    }

    [Fact]
    public async Task Submits_the_upsert_command_and_closes_with_the_result()
    {
        var mediator = new FakeMediator();
        UpsertOrderingPeriodCommand? sentCommand = null;
        mediator.Register<UpsertOrderingPeriodCommand, EbedrendeloApp.Common.Results.Result<OrderingPeriodDto>>(cmd =>
        {
            sentCommand = cmd;
            return EbedrendeloApp.Common.Results.Result.Success(new OrderingPeriodDto(
                1, cmd.Name, cmd.StartDate, cmd.EndDate, cmd.OrderDeadline, cmd.IsOpen, false));
        });
        Services.AddSingleton<IMediator>(mediator);

        var provider = Render<MudDialogProvider>((Bunit.ComponentParameterCollectionBuilder<MudDialogProvider> _) => { });
        var dialogService = Services.GetRequiredService<IDialogService>();

        // Use Existing so every field starts pre-filled — the dialog only leaves EndDate/OrderDeadline
        // null (and thus blocks submit) when the user hasn't picked them yet, which isn't what this
        // test is exercising.
        var existing = new OrderingPeriodDto(
            7, "Október", new DateOnly(2026, 10, 6), new DateOnly(2026, 11, 6), new DateTime(2026, 9, 26, 10, 0, 0), true, HasOrders: false);
        var parameters = new DialogParameters<OrderingPeriodDialog> { { x => x.Existing, existing } };

        await provider.InvokeAsync(() => dialogService.ShowAsync<OrderingPeriodDialog>("Időszak szerkesztése", parameters));

        var saveButton = provider.FindAll("button").First(b => b.TextContent.Contains("Mentés"));
        await provider.InvokeAsync(() => saveButton.Click());

        Assert.NotNull(sentCommand);
        Assert.Equal(new DateOnly(2026, 10, 6), sentCommand!.StartDate);
        Assert.Equal(7, sentCommand.Id);
    }
}
