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

    /// <summary>A havi mátrix hétvégét kihagyva sorolja fel a hónapot — a hétvégi tesztfutás miatt nem
    /// szabad simán <see cref="Today"/>-t használni egy havi-nézeti sor dátumaként, mert az véletlenül
    /// hétvégére eshet, és akkor sosem jelenne meg sorként.</summary>
    private static readonly DateOnly AWeekdayThisMonth = FirstWeekdayOfCurrentMonth();

    private static DateOnly FirstWeekdayOfCurrentMonth()
    {
        var date = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);
        while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            date = date.AddDays(1);
        }
        return date;
    }

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
    public void Shows_the_soup_portion_count_from_the_summary()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        var mediator = new FakeMediator();
        mediator.Register<GetALaCarteDailySummaryQuery, ALaCarteDailySummaryDto>(_ => new ALaCarteDailySummaryDto(
            Today, 5, [new ALaCarteSummaryLineDto(ALaCarteCategory.Foetel, "Rántott szelet", 5)]));
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<AdminALaCarteKitchenSummary>((ComponentParameterCollectionBuilder<AdminALaCarteKitchenSummary> _) => { });

        Assert.Contains("5", cut.Markup);
        Assert.Contains("levesadag", cut.Markup);
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
            var lines = new[] { new ALaCarteMonthlyLineDto(AWeekdayThisMonth, ALaCarteCategory.Foetel, "Havi rántott szelet", 42) };
            var offeredItems = new[] { new ALaCarteMonthlyOfferedItemDto(ALaCarteCategory.Foetel, "Havi rántott szelet") };
            return new ALaCarteMonthlySummaryDto(q.Year, q.Month, 42, lines, offeredItems);
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
    public void Havi_view_shows_an_empty_cell_for_an_item_offered_that_month_but_not_ordered_that_day()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        var mediator = new FakeMediator();
        mediator.Register<GetALaCarteDailySummaryQuery, ALaCarteDailySummaryDto>(_ => new ALaCarteDailySummaryDto(Today, 0, []));
        mediator.Register<GetALaCarteMonthlySummaryQuery, ALaCarteMonthlySummaryDto>(q =>
        {
            // "Sosem rendelt köret" tételt soha senki nem rendelte, bár egész hónapban kínálva volt —
            // ennek ellenére saját oszlopot kap, üres napi cellákkal (nem "0"-val, könnyebb olvasni).
            var lines = new[] { new ALaCarteMonthlyLineDto(AWeekdayThisMonth, ALaCarteCategory.Foetel, "Rántott szelet", 3) };
            var offeredItems = new[]
            {
                new ALaCarteMonthlyOfferedItemDto(ALaCarteCategory.Foetel, "Rántott szelet"),
                new ALaCarteMonthlyOfferedItemDto(ALaCarteCategory.Koret, "Sosem rendelt köret"),
            };
            return new ALaCarteMonthlySummaryDto(q.Year, q.Month, 3, lines, offeredItems);
        });
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<AdminALaCarteKitchenSummary>((ComponentParameterCollectionBuilder<AdminALaCarteKitchenSummary> _) => { });
        var haviChip = cut.FindAll(".mud-chip").First(c => c.TextContent.Trim() == "Havi");
        cut.InvokeAsync(() => haviChip.Click());

        Assert.Contains("Sosem rendelt köret", cut.Markup);

        var dayRowLabel = AWeekdayThisMonth.ToString("MM.dd. (ddd)", new System.Globalization.CultureInfo("hu-HU"));
        var dateCell = cut.FindAll("td.kitchen-monthly-table__date-cell").First(td => td.TextContent.Trim() == dayRowLabel);
        var cells = dateCell.ParentElement!.QuerySelectorAll("td");

        // cells[0] = dátum, cells[1] = Rántott szelet (Foetel, ábécé szerint elsőnek), cells[2] = Sosem
        // rendelt köret (Koret) — a mátrix oszlopsorrendje kategória, majd névsor szerinti.
        Assert.Equal("3", cells[1].TextContent.Trim());
        Assert.Equal(string.Empty, cells[2].TextContent.Trim());
    }

    [Fact]
    public void Havi_view_does_not_show_the_soup_portion_panel()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        var mediator = new FakeMediator();
        mediator.Register<GetALaCarteDailySummaryQuery, ALaCarteDailySummaryDto>(_ => new ALaCarteDailySummaryDto(Today, 0, []));
        mediator.Register<GetALaCarteMonthlySummaryQuery, ALaCarteMonthlySummaryDto>(q => new ALaCarteMonthlySummaryDto(q.Year, q.Month, 5, [], []));
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<AdminALaCarteKitchenSummary>((ComponentParameterCollectionBuilder<AdminALaCarteKitchenSummary> _) => { });
        var haviChip = cut.FindAll(".mud-chip").First(c => c.TextContent.Trim() == "Havi");
        cut.InvokeAsync(() => haviChip.Click());

        Assert.DoesNotContain("levesadag", cut.Markup);
    }

    [Fact]
    public void Clicking_a_week_row_collapses_its_daily_rows()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        var mediator = new FakeMediator();
        mediator.Register<GetALaCarteDailySummaryQuery, ALaCarteDailySummaryDto>(_ => new ALaCarteDailySummaryDto(Today, 0, []));
        var lines = new[] { new ALaCarteMonthlyLineDto(AWeekdayThisMonth, ALaCarteCategory.Foetel, "Rántott szelet", 3) };
        var offeredItems = new[] { new ALaCarteMonthlyOfferedItemDto(ALaCarteCategory.Foetel, "Rántott szelet") };
        mediator.Register<GetALaCarteMonthlySummaryQuery, ALaCarteMonthlySummaryDto>(q => new ALaCarteMonthlySummaryDto(q.Year, q.Month, 3, lines, offeredItems));
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<AdminALaCarteKitchenSummary>((ComponentParameterCollectionBuilder<AdminALaCarteKitchenSummary> _) => { });
        var haviChip = cut.FindAll(".mud-chip").First(c => c.TextContent.Trim() == "Havi");
        cut.InvokeAsync(() => haviChip.Click());

        var dayRowLabel = AWeekdayThisMonth.ToString("MM.dd. (ddd)", new System.Globalization.CultureInfo("hu-HU"));
        Assert.Contains(dayRowLabel, cut.Markup);

        // AWeekdayThisMonth a hónap első munkanapja, tehát mindig az első heti sorhoz tartozik.
        var firstWeekRow = cut.FindAll("tr.kitchen-monthly-table__week-row").First();
        cut.InvokeAsync(() => firstWeekRow.Click());

        Assert.DoesNotContain(dayRowLabel, cut.Markup);
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
