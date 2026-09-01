using Bunit;
using Bunit.TestDoubles;
using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Components.Pages.ALaCarte;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.ALaCarte.GetALaCarteDailySummary;
using EbedrendeloApp.Features.ALaCarte.GetALaCarteMonthlySummary;
using EbedrendeloApp.Tests.TestSupport;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace EbedrendeloApp.Tests.Components.ALaCarte;

public class AdminALaCarteKitchenSummaryTests : MudBunitContext
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Today);

    public AdminALaCarteKitchenSummaryTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Redirects_non_admin_users_to_the_today_menu_page()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(2, "Dolgozó Teszt", isAdmin: false));
        Services.AddSingleton<IMediator>(new FakeMediator());

        Render<AdminALaCarteKitchenSummary>((ComponentParameterCollectionBuilder<AdminALaCarteKitchenSummary> _) => { });

        var navigationManager = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/mai-menu", navigationManager.Uri);
    }

    [Fact]
    public void Shows_the_category_lines_with_their_counts()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        var mediator = new FakeMediator();
        mediator.Register<GetALaCarteDailySummaryQuery, ALaCarteDailySummaryDto>(_ => new ALaCarteDailySummaryDto(
            Today, 5,
            [
                new ALaCarteSummaryLineDto(ALaCarteCategory.Foetel, "Rántott szelet", 5),
                new ALaCarteSummaryLineDto(ALaCarteCategory.Koret, "Rizi-bizi", 3),
            ]));
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<AdminALaCarteKitchenSummary>((ComponentParameterCollectionBuilder<AdminALaCarteKitchenSummary> _) => { });

        Assert.Contains("Rántott szelet", cut.Markup);
        Assert.Contains("Rizi-bizi", cut.Markup);
    }

    [Fact]
    public void Switching_to_havi_loads_the_monthly_summary_instead_of_the_daily_one()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        var mediator = new FakeMediator();
        mediator.Register<GetALaCarteDailySummaryQuery, ALaCarteDailySummaryDto>(_ => new ALaCarteDailySummaryDto(
            Today, 0, [new ALaCarteSummaryLineDto(ALaCarteCategory.Foetel, "Csak ma", 1)]));
        GetALaCarteMonthlySummaryQuery? sentQuery = null;
        mediator.Register<GetALaCarteMonthlySummaryQuery, ALaCarteMonthlySummaryDto>(q =>
        {
            sentQuery = q;
            return new ALaCarteMonthlySummaryDto(q.Year, q.Month, [new ALaCarteSummaryLineDto(ALaCarteCategory.Foetel, "Havi rántott szelet", 42)]);
        });
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<AdminALaCarteKitchenSummary>((ComponentParameterCollectionBuilder<AdminALaCarteKitchenSummary> _) => { });
        Assert.Contains("Csak ma", cut.Markup);

        var haviChip = cut.FindAll(".mud-chip").First(c => c.TextContent.Trim() == "Havi");
        cut.InvokeAsync(() => haviChip.Click());

        Assert.NotNull(sentQuery);
        Assert.Equal(Today.Year, sentQuery!.Year);
        Assert.Equal(Today.Month, sentQuery.Month);
        Assert.Contains("Havi rántott szelet", cut.Markup);
        Assert.DoesNotContain("Csak ma", cut.Markup);
    }

    [Fact]
    public void Shows_a_no_orders_message_when_there_are_no_lines()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        var mediator = new FakeMediator();
        mediator.Register<GetALaCarteDailySummaryQuery, ALaCarteDailySummaryDto>(_ => new ALaCarteDailySummaryDto(Today, 0, []));
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<AdminALaCarteKitchenSummary>((ComponentParameterCollectionBuilder<AdminALaCarteKitchenSummary> _) => { });

        Assert.Contains("még nincs à la carte rendelés", cut.Markup);
    }
}
