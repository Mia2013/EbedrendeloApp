using Bunit;
using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Components.Pages.Admin;
using EbedrendeloApp.Features.Admin.GetAdminDashboard;
using EbedrendeloApp.Tests.TestSupport;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace EbedrendeloApp.Tests.Components.Admin;

public class AdminDashboardTests : MudBunitContext
{
    private static readonly DateOnly Today = new(2026, 9, 10);

    public AdminDashboardTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private static AdminDashboardDto Dashboard(IReadOnlyList<AdminTodoDto>? todos = null, int unpaidCount = 0) => new(
        Today: Today,
        ActivePeriod: new AdminPeriodDto(1, "2026. szeptember", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), IsOpen: true, RemainingWorkingDays: 15),
        Todos: todos ?? [],
        TodayMenuPortions: 42,
        TodayMenuVariants: [new AdminMenuVariantDto("A", "Rántott csirkemell", 30), new AdminMenuVariantDto("B", "Lecsó", 12)],
        TodayCancelledPortions: 2,
        MissingDailyMenuCount: 0,
        FirstMissingDailyMenu: null,
        TodayClosedByKitchen: false,
        UnpaidInvoiceCount: unpaidCount,
        UnpaidInvoiceTotalHuf: unpaidCount * 28000,
        WeeklyMenuPortions: Enumerable.Range(0, 5).Select(i => new AdminDailyPortionsDto(new DateOnly(2026, 9, 7).AddDays(i), 40)).ToList(),
        TodayALaCarteOffers: [new AdminALaCarteOfferDto("Túrógombóc", 3)],
        TodayALaCarteItemCount: 3,
        TodayALaCarteUserCount: 2,
        ALaCarteDeadline: new TimeOnly(10, 30),
        ALaCarteDeadlinePassed: false,
        NextWorkingDayHasALaCarteOffer: true,
        WeeklyALaCarte: Enumerable.Range(0, 5).Select(i => new AdminALaCarteDayDto(new DateOnly(2026, 9, 7).AddDays(i), 2, 1, 1)).ToList(),
        Tiles: new AdminTileStatsDto(1, 2, 300, 12, 4, "B", 0));

    private void RegisterAdmin(AdminDashboardDto dashboard)
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        var mediator = new FakeMediator();
        mediator.Register<GetAdminDashboardQuery, AdminDashboardDto>(_ => dashboard);
        Services.AddSingleton<IMediator>(mediator);
    }

    private IRenderedComponent<AdminDashboard> RenderPage()
        => Render<AdminDashboard>((ComponentParameterCollectionBuilder<AdminDashboard> _) => { });

    [Fact]
    public void Redirects_non_admin_users_to_the_today_menu_page()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(2, "Dolgozó Teszt", isAdmin: false));
        Services.AddSingleton<IMediator>(new FakeMediator());

        RenderPage();

        var navigationManager = (Bunit.TestDoubles.BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/mai-menu", navigationManager.Uri);
    }

    [Fact]
    public void Shows_today_portions_the_variant_breakdown_and_the_active_period()
    {
        RegisterAdmin(Dashboard());

        var cut = RenderPage();

        Assert.Contains("42", cut.Markup);
        Assert.Contains("A 30 · B 12", cut.Markup);
        Assert.Contains("2026. szeptember", cut.Markup);
        Assert.Contains("15 munkanap van hátra", cut.Markup);
    }

    [Fact]
    public void Without_todos_the_all_clear_message_is_shown()
    {
        RegisterAdmin(Dashboard());

        var cut = RenderPage();

        Assert.Contains("Ma nincs elmaradás", cut.Markup);
    }

    [Fact]
    public void Each_todo_is_listed_with_its_title_and_a_link_to_the_page_that_resolves_it()
    {
        RegisterAdmin(Dashboard(
            todos: [new AdminTodoDto(AdminTodoKind.UnpaidInvoices, Count: 2, AmountHuf: 56000, Dates: [])],
            unpaidCount: 2));

        var cut = RenderPage();

        Assert.DoesNotContain("Ma nincs elmaradás", cut.Markup);
        Assert.Contains("2 kiállított számla fizetetlen", cut.Markup);
        Assert.NotEmpty(cut.FindAll("a[href='szamlak']"));
    }

    [Fact]
    public void Clicking_an_area_tile_navigates_to_its_page()
    {
        RegisterAdmin(Dashboard());

        var cut = RenderPage();
        var billingTile = cut.FindAll(".mud-paper").First(p => p.TextContent.Contains("Számlák · Generálás"));
        billingTile.Click();

        var navigationManager = (Bunit.TestDoubles.BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/szamlak", navigationManager.Uri);
    }
}
