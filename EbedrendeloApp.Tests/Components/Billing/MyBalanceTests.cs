using Bunit;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Components.Pages.Billing;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Billing.GetMyBalance;
using EbedrendeloApp.Features.Billing.GetMyCreditLedger;
using EbedrendeloApp.Tests.TestSupport;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace EbedrendeloApp.Tests.Components.Billing;

public class MyBalanceTests : MudBunitContext
{
    private static CreditLedgerEntryDto Entry(int amountHuf, CreditEntryKind kind = CreditEntryKind.CancellationCredit, string? note = null) =>
        new(1, kind, amountHuf, Math.Max(amountHuf, 0), DateTime.UtcNow, note, 2, "Nagy Éva", null, null, null, null, null);

    public MyBalanceTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Kovács János", isAdmin: false));
    }

    private IRenderedComponent<MyBalance> RenderWith(FakeMediator mediator)
    {
        Services.AddSingleton<IMediator>(mediator);
        return Render<MyBalance>();
    }

    [Fact]
    public void Shows_the_current_balance_from_the_query()
    {
        var mediator = new FakeMediator();
        mediator.Register<GetMyBalanceQuery, Result<int>>(_ => Result.Success(2200));
        mediator.Register<GetMyCreditLedgerQuery, Result<IReadOnlyList<CreditLedgerEntryDto>>>(_ => Result.Success<IReadOnlyList<CreditLedgerEntryDto>>([]));

        var cut = RenderWith(mediator);

        Assert.Contains("2\u00A0200 Ft", cut.Markup);
    }

    [Fact]
    public void Shows_a_muted_zero_balance_without_error()
    {
        var mediator = new FakeMediator();
        mediator.Register<GetMyBalanceQuery, Result<int>>(_ => Result.Success(0));
        mediator.Register<GetMyCreditLedgerQuery, Result<IReadOnlyList<CreditLedgerEntryDto>>>(_ => Result.Success<IReadOnlyList<CreditLedgerEntryDto>>([]));

        var cut = RenderWith(mediator);

        Assert.Contains("0 Ft", cut.Markup);
    }

    [Fact]
    public void Lists_ledger_entries_with_kind_chip_and_description()
    {
        var mediator = new FakeMediator();
        mediator.Register<GetMyBalanceQuery, Result<int>>(_ => Result.Success(500));
        mediator.Register<GetMyCreditLedgerQuery, Result<IReadOnlyList<CreditLedgerEntryDto>>>(
            _ => Result.Success<IReadOnlyList<CreditLedgerEntryDto>>([Entry(500, CreditEntryKind.ManualAdjustment, note: "Konyhai üzemzavar")]));

        var cut = RenderWith(mediator);

        Assert.Contains("Kézi korrekció", cut.Markup);
        Assert.Contains("Konyhai üzemzavar", cut.Markup);
    }

    [Fact]
    public void Shows_positive_amounts_with_a_plus_prefix_and_negative_without()
    {
        var mediator = new FakeMediator();
        mediator.Register<GetMyBalanceQuery, Result<int>>(_ => Result.Success(0));
        mediator.Register<GetMyCreditLedgerQuery, Result<IReadOnlyList<CreditLedgerEntryDto>>>(_ => Result.Success<IReadOnlyList<CreditLedgerEntryDto>>(
        [
            Entry(1400, CreditEntryKind.CancellationCredit),
            Entry(-1400, CreditEntryKind.CreditRevoked),
        ]));

        var cut = RenderWith(mediator);

        Assert.Contains("+1\u00A0400 Ft", cut.Markup);
        Assert.Contains("-1\u00A0400 Ft", cut.Markup);
    }

    [Fact]
    public void Shows_empty_state_text_when_the_ledger_has_no_entries()
    {
        var mediator = new FakeMediator();
        mediator.Register<GetMyBalanceQuery, Result<int>>(_ => Result.Success(0));
        mediator.Register<GetMyCreditLedgerQuery, Result<IReadOnlyList<CreditLedgerEntryDto>>>(_ => Result.Success<IReadOnlyList<CreditLedgerEntryDto>>([]));

        var cut = RenderWith(mediator);

        Assert.Contains("Még nincs jóváírás-tételed.", cut.Markup);
    }

    [Fact]
    public void Shows_the_menu_scope_disclaimer_text()
    {
        var mediator = new FakeMediator();
        mediator.Register<GetMyBalanceQuery, Result<int>>(_ => Result.Success(0));
        mediator.Register<GetMyCreditLedgerQuery, Result<IReadOnlyList<CreditLedgerEntryDto>>>(_ => Result.Success<IReadOnlyList<CreditLedgerEntryDto>>([]));

        var cut = RenderWith(mediator);

        Assert.Contains("kizárólag menürendelésre számítható be", cut.Markup);
    }
}
