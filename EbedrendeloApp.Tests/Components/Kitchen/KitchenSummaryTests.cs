using Bunit;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Components.Pages.Kitchen;
using EbedrendeloApp.Features.Kitchen;
using EbedrendeloApp.Features.Kitchen.CloseDay;
using EbedrendeloApp.Features.Kitchen.GetKitchenClosure;
using EbedrendeloApp.Features.Kitchen.GetKitchenSummary;
using EbedrendeloApp.Features.Kitchen.GetKitchenSummaryRange;
using EbedrendeloApp.Tests.TestSupport;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace EbedrendeloApp.Tests.Components.Kitchen;

public class KitchenSummaryTests : MudBunitContext
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Today);

    public KitchenSummaryTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private static FakeMediator MediatorWithDay(KitchenSummaryDto summary, KitchenClosureDto? closure = null)
    {
        var mediator = new FakeMediator();
        mediator.Register<GetKitchenSummaryQuery, KitchenSummaryDto>(_ => summary);
        mediator.Register<GetKitchenClosureQuery, KitchenClosureDto?>(_ => closure);
        return mediator;
    }

    [Fact]
    public void Redirects_non_admin_users_to_the_today_menu_page()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(2, "Dolgozó Teszt", isAdmin: false));
        Services.AddSingleton<IMediator>(new FakeMediator());

        Render<KitchenSummary>((ComponentParameterCollectionBuilder<KitchenSummary> _) => { });

        var navigationManager = (Bunit.TestDoubles.BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/mai-menu", navigationManager.Uri);
    }

    [Fact]
    public void Shows_the_variant_lines_and_the_total()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        var summary = new KitchenSummaryDto(Today, IsClosed: false,
        [
            new KitchenVariantLineDto("A", "Gulyásleves + Rántott hús", 18),
            new KitchenVariantLineDto("B", "Gulyásleves + Halászlé", 11),
        ], TotalPortions: 29);
        Services.AddSingleton<IMediator>(MediatorWithDay(summary));

        var cut = Render<KitchenSummary>((ComponentParameterCollectionBuilder<KitchenSummary> _) => { });

        Assert.Contains("Gulyásleves + Rántott hús", cut.Markup);
        Assert.Contains("Gulyásleves + Halászlé", cut.Markup);
        Assert.Contains("29", cut.Markup);
        Assert.Contains("Nyitva", cut.Markup);
    }

    [Fact]
    public void An_open_day_shows_the_close_day_button_and_not_the_reopen_button()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        var summary = new KitchenSummaryDto(Today, IsClosed: false, [], 0);
        Services.AddSingleton<IMediator>(MediatorWithDay(summary));

        var cut = Render<KitchenSummary>((ComponentParameterCollectionBuilder<KitchenSummary> _) => { });

        Assert.Contains("Nap lezárása", cut.Markup);
        Assert.DoesNotContain("Nap újranyitása", cut.Markup);
    }

    [Fact]
    public void A_closed_day_shows_the_reopen_button_and_the_closure_audit_line()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        var summary = new KitchenSummaryDto(Today, IsClosed: true, [new KitchenVariantLineDto("A", "Gulyásleves", 5)], 5);
        var closure = new KitchenClosureDto(
            Today, new DateTime(2026, 9, 9, 11, 15, 0, DateTimeKind.Utc), 7, "Nagy Anna",
            5, [new KitchenClosureLineDto("A", "Gulyásleves", 5)], null, null, null);
        Services.AddSingleton<IMediator>(MediatorWithDay(summary, closure));

        var cut = Render<KitchenSummary>((ComponentParameterCollectionBuilder<KitchenSummary> _) => { });

        Assert.Contains("Nap újranyitása", cut.Markup);
        Assert.DoesNotContain("Nap lezárása<", cut.Markup);
        Assert.Contains("Nagy Anna", cut.Markup);
        Assert.Contains("Zárva", cut.Markup);
    }

    [Fact]
    public void A_reopened_day_shows_both_the_close_and_reopen_audit_lines()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        var summary = new KitchenSummaryDto(Today, IsClosed: false, [new KitchenVariantLineDto("A", "Gulyásleves", 8)], 8);
        var closure = new KitchenClosureDto(
            Today, new DateTime(2026, 9, 8, 11, 0, 0, DateTimeKind.Utc), 7, "Nagy Anna",
            5, [new KitchenClosureLineDto("A", "Gulyásleves", 5)],
            new DateTime(2026, 9, 8, 14, 30, 0, DateTimeKind.Utc), 9, "Kovács János");
        Services.AddSingleton<IMediator>(MediatorWithDay(summary, closure));

        var cut = Render<KitchenSummary>((ComponentParameterCollectionBuilder<KitchenSummary> _) => { });

        Assert.Contains("Nagy Anna", cut.Markup);
        Assert.Contains("Kovács János", cut.Markup);
        Assert.Contains("Nap lezárása", cut.Markup);
        Assert.Contains("korábbi zárás pillanatképe", cut.Markup);
    }

    [Fact]
    public void Switching_to_idoszaki_loads_the_range_summary()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        var mediator = MediatorWithDay(new KitchenSummaryDto(Today, false, [], 0));
        GetKitchenSummaryRangeQuery? sentQuery = null;
        mediator.Register<GetKitchenSummaryRangeQuery, IReadOnlyList<KitchenSummaryDto>>(q =>
        {
            sentQuery = q;
            return [new KitchenSummaryDto(Today, false, [new KitchenVariantLineDto("A", "Gulyásleves", 4)], 4)];
        });
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<KitchenSummary>((ComponentParameterCollectionBuilder<KitchenSummary> _) => { });

        var idoszakiChip = cut.FindAll(".mud-chip").First(c => c.TextContent.Trim() == "Időszaki");
        cut.InvokeAsync(() => idoszakiChip.Click());

        Assert.NotNull(sentQuery);
        Assert.Contains("A: 4", cut.Markup);
    }

    [Fact]
    public async Task Confirming_the_close_dialog_sends_the_close_command_and_reloads()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(7, "Admin Teszt", isAdmin: true));
        var mediator = new FakeMediator();
        var openSummary = new KitchenSummaryDto(Today, false, [new KitchenVariantLineDto("A", "Gulyásleves", 3)], 3);
        var closedSummary = openSummary with { IsClosed = true };
        var callCount = 0;
        mediator.Register<GetKitchenSummaryQuery, KitchenSummaryDto>(_ => callCount++ == 0 ? openSummary : closedSummary);
        mediator.Register<GetKitchenClosureQuery, KitchenClosureDto?>(_ => (KitchenClosureDto?)null);
        CloseDayCommand? sentCommand = null;
        mediator.Register<CloseDayCommand, Result<KitchenClosureDto>>(cmd =>
        {
            sentCommand = cmd;
            return Result.Success(new KitchenClosureDto(
                Today, DateTime.UtcNow, 7, "Admin Teszt", 3, [new KitchenClosureLineDto("A", "Gulyásleves", 3)], null, null, null));
        });
        Services.AddSingleton<IMediator>(mediator);

        var dialogProvider = Render<MudDialogProvider>();
        var cut = Render<KitchenSummary>((ComponentParameterCollectionBuilder<KitchenSummary> _) => { });

        var closeButton = cut.FindAll("button").First(b => b.TextContent.Contains("Nap lezárása"));
        await cut.InvokeAsync(() => closeButton.Click());

        var confirmButton = dialogProvider.FindAll("button").First(b => b.TextContent.Contains("Lezárás"));
        await dialogProvider.InvokeAsync(() => confirmButton.Click());

        Assert.NotNull(sentCommand);
        Assert.Equal(Today, sentCommand!.Date);
        Assert.Equal(7, sentCommand.ClosedByUserId);
    }
}
