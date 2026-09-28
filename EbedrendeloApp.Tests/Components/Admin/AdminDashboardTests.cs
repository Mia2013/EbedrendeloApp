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

    private static AdminDashboardDto Dashboard(
        IReadOnlyList<AdminTodoDto>? todos = null,
        int unpaidCount = 0,
        int missingMenuCount = 0,
        bool todayIsServiceDay = true) => new(
        Today: Today,
        TodayIsServiceDay: todayIsServiceDay,
        ActivePeriod: new AdminPeriodDto("Őszi időszak", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), IsOpen: true, RemainingWorkingDays: 15),
        Todos: todos ?? [],
        TodayMenuPortions: 37,
        TodayMenuVariants: [new AdminMenuVariantDto("A", "Rántott csirkemell", 25), new AdminMenuVariantDto("B", "Lecsó", 12)],
        TodayCancelledPortions: 2,
        MissingDailyMenuCount: missingMenuCount,
        FirstMissingDailyMenu: missingMenuCount > 0 ? Today.AddDays(1) : null,
        TodayClosedByKitchen: false,
        UnpaidInvoiceCount: unpaidCount,
        UnpaidInvoiceTotalHuf: unpaidCount * 28000,
        TodayALaCarteOffers: [new AdminALaCarteOfferDto("Túrógombóc", 3)],
        TodayALaCarteItemCount: 3,
        TodayALaCarteUserCount: 2,
        ALaCarteDeadline: new TimeOnly(10, 30),
        ALaCarteDeadlinePassed: false,
        NextALaCarteDay: Today.AddDays(1),
        NextALaCarteDayHasOffer: true,
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

    /// <summary>A KPI-kártya nagy számát a kártya felirata alapján keressük, nem a teljes markupban —
    /// ugyanaz a szám máshol (feliratban, SVG-útvonalban) is előfordulhat.</summary>
    private static AngleSharp.Dom.IElement KpiCard(IRenderedComponent<AdminDashboard> cut, string overline)
        => cut.FindAll(".mud-paper").First(p =>
            p.QuerySelector(".mud-typography-overline")?.TextContent.Trim() == overline);

    private static string KpiValue(IRenderedComponent<AdminDashboard> cut, string overline)
        => KpiCard(cut, overline).QuerySelector(".mud-typography-h4, .mud-typography-h5")!.TextContent.Trim();

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

        Assert.Equal("37", KpiValue(cut, "Mai adagok"));
        Assert.Contains("A 25 · B 12", cut.Markup);
        Assert.Equal("Őszi időszak", KpiValue(cut, "Aktív időszak"));
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
            todos:
            [
                new AdminTodoDto(AdminTodoKind.UnpaidInvoices, Count: 2, AmountHuf: 56000, Dates: []),
                new AdminTodoDto(AdminTodoKind.MissingALaCarteOffer, Count: 0, AmountHuf: 0, Dates: [Today.AddDays(1)]),
            ],
            unpaidCount: 2));

        var cut = RenderPage();

        Assert.DoesNotContain("Ma nincs elmaradás", cut.Markup);
        Assert.Contains("2 kiállított számla fizetetlen", cut.Markup);
        Assert.Contains("Számlák", cut.Find("a.mud-button-root[href='szamlak']").TextContent);
        // A kínálat-teendő a hiányzó napra nyitja az oldalt, nem a mára.
        Assert.NotNull(cut.Find("a.mud-button-root[href='alacarte-napi-kinalat?datum=2026-09-11']"));
    }

    [Fact]
    public void Kpi_cards_stay_neutral_when_there_is_nothing_to_do()
    {
        RegisterAdmin(Dashboard(unpaidCount: 0, missingMenuCount: 0));

        var cut = RenderPage();

        foreach (var overline in new[] { "Menü nélküli munkanap", "Fizetetlen számla" })
        {
            var card = KpiCard(cut, overline);
            Assert.Empty(card.QuerySelectorAll(".mud-warning-text, .mud-error-text"));
        }
    }

    [Fact]
    public void On_a_non_service_day_the_missing_menu_and_offer_warnings_are_not_shown()
    {
        var dashboard = Dashboard(todayIsServiceDay: false) with { TodayMenuVariants = [], TodayALaCarteOffers = [] };
        RegisterAdmin(dashboard);

        var cut = RenderPage();

        Assert.DoesNotContain("Mára nincs publikált napi menü", cut.Markup);
        Assert.DoesNotContain("Mára nincs kiajánlva", cut.Markup);
        Assert.Contains("Ma nincs kiszolgálás", cut.Markup);
    }

    [Fact]
    public void Area_tiles_are_real_links_to_their_pages()
    {
        RegisterAdmin(Dashboard());

        var cut = RenderPage();

        // Valódi <a href>: billentyűzettel elérhető és új lapon is nyitható.
        var billingTile = cut.Find("a[href='szamlak']:not(.mud-button-root)");
        Assert.Contains("Számlák", billingTile.TextContent);
    }
}
