using Bunit;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Components.Pages.Billing;
using EbedrendeloApp.Features.Billing.GetMyInvoices;
using EbedrendeloApp.Tests.TestSupport;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace EbedrendeloApp.Tests.Components.Billing;

public class MyInvoicesTests : MudBunitContext
{
    public MyInvoicesTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(5, "Nagy Béla", isAdmin: false));
    }

    [Fact]
    public void Shows_no_invoices_message_when_there_are_none()
    {
        var mediator = new FakeMediator();
        mediator.Register<GetMyInvoicesQuery, Result<IReadOnlyList<MyInvoiceDto>>>(_ => Result.Success<IReadOnlyList<MyInvoiceDto>>([]));
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<MyInvoices>((ComponentParameterCollectionBuilder<MyInvoices> _) => { });

        Assert.Contains("Még nincs számlád.", cut.Markup);
    }

    [Fact]
    public void Shows_the_breakdown_the_paid_status_and_the_credit_note()
    {
        var invoice = new MyInvoiceDto(
            Id: 1, OrderingPeriodId: 1, PeriodName: "2026. szeptember",
            PeriodStartDate: new DateOnly(2026, 9, 1), PeriodEndDate: new DateOnly(2026, 9, 30),
            MenuGrossHuf: 28000, ALaCarteGrossHuf: 4500, GrossHuf: 32500, CreditAppliedHuf: 3000,
            MenuPayableHuf: 25000, ALaCartePayableHuf: 4500, PayableHuf: 29500,
            IsPaid: false, PaidAtUtc: null, GeneratedAtUtc: new DateTime(2026, 9, 16, 9, 0, 0));

        var mediator = new FakeMediator();
        mediator.Register<GetMyInvoicesQuery, Result<IReadOnlyList<MyInvoiceDto>>>(_ => Result.Success<IReadOnlyList<MyInvoiceDto>>([invoice]));
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<MyInvoices>((ComponentParameterCollectionBuilder<MyInvoices> _) => { });

        Assert.Contains("2026. szeptember", cut.Markup);
        Assert.Contains("28 000 Ft", cut.Markup);
        Assert.Contains("4 500 Ft", cut.Markup);
        Assert.Contains("29 500 Ft", cut.Markup);
        Assert.Contains("Fizetetlen", cut.Markup);
        Assert.Contains("jóváírás került beszámításra", cut.Markup);
    }

    [Fact]
    public void A_paid_invoice_shows_the_paid_chip_and_no_credit_note_when_nothing_was_applied()
    {
        var invoice = new MyInvoiceDto(
            Id: 2, OrderingPeriodId: 2, PeriodName: "2026. augusztus",
            PeriodStartDate: new DateOnly(2026, 8, 1), PeriodEndDate: new DateOnly(2026, 8, 31),
            MenuGrossHuf: 26600, ALaCarteGrossHuf: 0, GrossHuf: 26600, CreditAppliedHuf: 0,
            MenuPayableHuf: 26600, ALaCartePayableHuf: 0, PayableHuf: 26600,
            IsPaid: true, PaidAtUtc: new DateTime(2026, 8, 20, 14, 32, 0), GeneratedAtUtc: new DateTime(2026, 8, 16, 9, 0, 0));

        var mediator = new FakeMediator();
        mediator.Register<GetMyInvoicesQuery, Result<IReadOnlyList<MyInvoiceDto>>>(_ => Result.Success<IReadOnlyList<MyInvoiceDto>>([invoice]));
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<MyInvoices>((ComponentParameterCollectionBuilder<MyInvoices> _) => { });

        Assert.Contains("Fizetve", cut.Markup);
        Assert.Contains("2026. augusztus", cut.Markup);
        Assert.DoesNotContain("jóváírás került beszámításra", cut.Markup);
    }
}
