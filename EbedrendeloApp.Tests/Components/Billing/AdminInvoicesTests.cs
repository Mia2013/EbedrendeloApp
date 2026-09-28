using Bunit;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Components.Pages.Billing;
using EbedrendeloApp.Features.Billing.GeneratePeriodInvoices;
using EbedrendeloApp.Features.Billing.GetInvoices;
using EbedrendeloApp.Features.Billing.MarkInvoicePaid;
using EbedrendeloApp.Features.Calendar;
using EbedrendeloApp.Features.Calendar.GetOrderingPeriods;
using EbedrendeloApp.Tests.TestSupport;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace EbedrendeloApp.Tests.Components.Billing;

public class AdminInvoicesTests : MudBunitContext
{
    private static readonly OrderingPeriodDto Period = new(
        1, "2026. szeptember", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), new DateTime(2026, 8, 15, 10, 0, 0), true, true);

    private static readonly InvoiceDto Invoice = new(
        Id: 10, UserId: 5, UserDisplayName: "Nagy Béla", OrderingPeriodId: 1, PeriodName: "2026. szeptember",
        SequenceNumber: 1, DayCount: 20, GrossHuf: 28000, CreditAppliedHuf: 3000, PayableHuf: 25000,
        IsPaid: false, PaidAtUtc: null, GeneratedAtUtc: new DateTime(2026, 9, 16, 9, 0, 0));

    public AdminInvoicesTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private static FakeMediator MediatorWithInvoices(IReadOnlyList<InvoiceDto> invoices)
    {
        var mediator = new FakeMediator();
        mediator.Register<GetOrderingPeriodsQuery, IReadOnlyList<OrderingPeriodDto>>(_ => [Period]);
        mediator.Register<GetInvoicesQuery, IReadOnlyList<InvoiceDto>>(_ => invoices);
        return mediator;
    }

    [Fact]
    public void Redirects_non_admin_users_to_the_today_menu_page()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(2, "Dolgozó Teszt", isAdmin: false));
        Services.AddSingleton<IMediator>(new FakeMediator());

        Render<AdminInvoices>((ComponentParameterCollectionBuilder<AdminInvoices> _) => { });

        var navigationManager = (Bunit.TestDoubles.BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/mai-menu", navigationManager.Uri);
    }

    [Fact]
    public void Shows_the_full_breakdown_and_status_for_each_invoice()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        Services.AddSingleton<IMediator>(MediatorWithInvoices([Invoice]));

        var cut = Render<AdminInvoices>((ComponentParameterCollectionBuilder<AdminInvoices> _) => { });

        Assert.Contains("Nagy Béla", cut.Markup);
        Assert.Contains("28 000 Ft", cut.Markup);
        Assert.Contains("3 000 Ft", cut.Markup);
        Assert.Contains("25 000 Ft", cut.Markup);
        Assert.Contains("Fizetetlen", cut.Markup);
    }

    [Fact]
    public void Selecting_the_fizetve_filter_requeries_with_paid_status()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        var mediator = MediatorWithInvoices([Invoice]);
        GetInvoicesQuery? lastQuery = null;
        mediator.Register<GetInvoicesQuery, IReadOnlyList<InvoiceDto>>(q =>
        {
            lastQuery = q;
            return [Invoice];
        });
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<AdminInvoices>((ComponentParameterCollectionBuilder<AdminInvoices> _) => { });
        var paidChip = cut.FindAll(".mud-chip").First(c => c.TextContent.Trim() == "Fizetve");
        cut.InvokeAsync(() => paidChip.Click());

        Assert.NotNull(lastQuery);
        Assert.Equal(true, lastQuery!.IsPaid);
    }

    [Fact]
    public void Changing_the_period_filter_requeries_with_the_selected_period()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        var mediator = MediatorWithInvoices([Invoice]);
        GetInvoicesQuery? lastQuery = null;
        mediator.Register<GetInvoicesQuery, IReadOnlyList<InvoiceDto>>(q =>
        {
            lastQuery = q;
            return [Invoice];
        });
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<AdminInvoices>((ComponentParameterCollectionBuilder<AdminInvoices> _) => { });
        var filterSelect = cut.FindComponents<MudSelect<int?>>().Single();
        cut.InvokeAsync(() => filterSelect.Instance.ValueChanged.InvokeAsync(Period.Id));

        Assert.NotNull(lastQuery);
        Assert.Equal(Period.Id, lastQuery!.OrderingPeriodId);
    }

    [Fact]
    public async Task Generating_invoices_shows_the_summary_and_reloads_the_list()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(7, "Admin Teszt", isAdmin: true));
        var mediator = MediatorWithInvoices([]);
        GeneratePeriodInvoicesCommand? sentCommand = null;
        mediator.Register<GeneratePeriodInvoicesCommand, Result<BatchInvoiceResult>>(cmd =>
        {
            sentCommand = cmd;
            return Result.Success(new BatchInvoiceResult([
                new GeneratedInvoiceDto(InvoiceId: 10, UserId: 5, SequenceNumber: 1, DayCount: 20, GrossHuf: 28000, CreditAppliedHuf: 0, PayableHuf: 28000),
                new GeneratedInvoiceDto(InvoiceId: 11, UserId: 6, SequenceNumber: 2, DayCount: 2, GrossHuf: 2800, CreditAppliedHuf: 0, PayableHuf: 2800),
            ]));
        });
        Services.AddSingleton<IMediator>(mediator);

        var dialogProvider = Render<MudDialogProvider>();
        var cut = Render<AdminInvoices>((ComponentParameterCollectionBuilder<AdminInvoices> _) => { });

        var generateButton = cut.FindAll("button").First(b => b.TextContent.Contains("Számlák generálása"));
        await cut.InvokeAsync(() => generateButton.Click());

        var confirmButton = dialogProvider.FindAll("button").First(b => b.TextContent.Contains("Generálás"));
        await dialogProvider.InvokeAsync(() => confirmButton.Click());

        Assert.NotNull(sentCommand);
        Assert.Equal(Period.Id, sentCommand!.OrderingPeriodId);
        Assert.Equal(7, sentCommand.GeneratedByUserId);
        Assert.Contains("2 számla legenerálva", cut.Markup);
        Assert.Contains("1 kiegészítő számla", cut.Markup);
    }

    [Fact]
    public async Task Marking_an_invoice_paid_sends_the_command_and_reloads()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(7, "Admin Teszt", isAdmin: true));
        var mediator = MediatorWithInvoices([Invoice]);
        MarkInvoicePaidCommand? sentCommand = null;
        mediator.Register<MarkInvoicePaidCommand, Result>(cmd =>
        {
            sentCommand = cmd;
            return Result.Success();
        });
        Services.AddSingleton<IMediator>(mediator);

        var dialogProvider = Render<MudDialogProvider>();
        var cut = Render<AdminInvoices>((ComponentParameterCollectionBuilder<AdminInvoices> _) => { });

        var payButton = cut.FindAll("button[title='Fizetettnek jelölés']").Single();
        await cut.InvokeAsync(() => payButton.Click());

        var confirmButton = dialogProvider.FindAll("button").First(b => b.TextContent.Contains("Fizetettnek jelölés"));
        await dialogProvider.InvokeAsync(() => confirmButton.Click());

        Assert.NotNull(sentCommand);
        Assert.Equal(Invoice.Id, sentCommand!.InvoiceId);
        Assert.Equal(7, sentCommand.MarkedByUserId);
    }
}
