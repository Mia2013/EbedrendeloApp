using Bunit;
using Bunit.TestDoubles;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Components.Pages.Menus;
using EbedrendeloApp.Features.Calendar;
using EbedrendeloApp.Features.Calendar.GetOrderingPeriods;
using EbedrendeloApp.Features.Menus;
using EbedrendeloApp.Features.Menus.DeleteDailyMenu;
using EbedrendeloApp.Features.Menus.GetDailyMenu;
using EbedrendeloApp.Features.Menus.GetMenuDishSuggestions;
using EbedrendeloApp.Features.Menus.GetPeriodMenu;
using EbedrendeloApp.Features.Menus.UpsertDailyMenu;
using EbedrendeloApp.Tests.TestSupport;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace EbedrendeloApp.Tests.Components.Menus;

public class DailyMenuEditorTests : MudBunitContext
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Today);

    // A Monday-Friday work week safely in the future/past of "today", regardless of which weekday the
    // test happens to run on — every row in it is deterministically editable / view-only.
    private static readonly DateOnly FutureWeekStart = NextMonday(Today.AddDays(14));
    private static readonly DateOnly FutureWeekEnd = FutureWeekStart.AddDays(4);
    private static readonly DateOnly PastWeekStart = PreviousMonday(Today.AddDays(-14));
    private static readonly DateOnly PastWeekEnd = PastWeekStart.AddDays(4);

    public DailyMenuEditorTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private static DateOnly NextMonday(DateOnly from) => from.AddDays(((int)DayOfWeek.Monday - (int)from.DayOfWeek + 7) % 7);

    private static DateOnly PreviousMonday(DateOnly from) => from.AddDays(-(((int)from.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7));

    private static OrderingPeriodDto FuturePeriod(int id = 1) =>
        new(id, "Jövő hét", FutureWeekStart, FutureWeekEnd, FutureWeekStart.AddDays(-10).ToDateTime(new TimeOnly(10, 0)), true, false);

    private static OrderingPeriodDto PastPeriod(int id = 2) =>
        new(id, "Múlt hét", PastWeekStart, PastWeekEnd, PastWeekStart.AddDays(-10).ToDateTime(new TimeOnly(10, 0)), true, false);

    private static FakeMediator BaseMediator(params OrderingPeriodDto[] periodList)
    {
        var mediator = new FakeMediator();
        mediator.Register<GetOrderingPeriodsQuery, IReadOnlyList<OrderingPeriodDto>>(_ => periodList);
        return mediator;
    }

    [Fact]
    public void Redirects_non_admin_users_to_the_today_menu_page()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(2, "Dolgozó Teszt", isAdmin: false));
        Services.AddSingleton<IMediator>(new FakeMediator());

        Render<DailyMenuEditor>((ComponentParameterCollectionBuilder<DailyMenuEditor> _) => { });

        var navigationManager = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/mai-menu", navigationManager.Uri);
    }

    [Fact]
    public void Shows_a_message_when_there_are_no_ordering_periods()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        Services.AddSingleton<IMediator>(BaseMediator());

        var cut = Render<DailyMenuEditor>((ComponentParameterCollectionBuilder<DailyMenuEditor> _) => { });

        Assert.Contains("Még nincs rendelési időszak.", cut.Markup);
    }

    [Fact]
    public void Lists_exactly_the_selected_periods_five_workdays_with_no_menu_by_default()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        var mediator = BaseMediator(FuturePeriod());
        mediator.Register<GetPeriodMenuQuery, Result<IReadOnlyList<DailyMenuDto>>>(_ => Result.Success<IReadOnlyList<DailyMenuDto>>([]));
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<DailyMenuEditor>((ComponentParameterCollectionBuilder<DailyMenuEditor> _) => { });

        var noMenuCount = cut.Markup.Split("Nincs még menü erre a napra.", StringSplitOptions.None).Length - 1;
        Assert.Equal(5, noMenuCount);
        var hu = System.Globalization.CultureInfo.GetCultureInfo("hu-HU");
        Assert.Contains(FutureWeekStart.ToString("MM.dd.", hu), cut.Markup);
        Assert.Contains(FutureWeekEnd.ToString("MM.dd.", hu), cut.Markup);
    }

    [Fact]
    public void Past_days_have_no_edit_button()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        var mediator = BaseMediator(PastPeriod());
        mediator.Register<GetPeriodMenuQuery, Result<IReadOnlyList<DailyMenuDto>>>(_ => Result.Success<IReadOnlyList<DailyMenuDto>>([]));
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<DailyMenuEditor>((ComponentParameterCollectionBuilder<DailyMenuEditor> _) => { });

        Assert.Empty(cut.FindAll("button[title='Szerkesztés']"));
    }

    [Fact]
    public void Past_days_show_a_disabled_edit_button_as_the_not_editable_hint()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        var mediator = BaseMediator(PastPeriod());
        mediator.Register<GetPeriodMenuQuery, Result<IReadOnlyList<DailyMenuDto>>>(_ => Result.Success<IReadOnlyList<DailyMenuDto>>([]));
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<DailyMenuEditor>((ComponentParameterCollectionBuilder<DailyMenuEditor> _) => { });

        var hintButtons = cut.FindAll("button[title='Elmúlt, csak megtekinthető']");
        Assert.Equal(5, hintButtons.Count);
        Assert.All(hintButtons, b => Assert.True(b.HasAttribute("disabled")));
    }

    [Fact]
    public void Past_days_show_a_disabled_delete_button_too()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        var mediator = BaseMediator(PastPeriod());
        mediator.Register<GetPeriodMenuQuery, Result<IReadOnlyList<DailyMenuDto>>>(_ => Result.Success<IReadOnlyList<DailyMenuDto>>([]));
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<DailyMenuEditor>((ComponentParameterCollectionBuilder<DailyMenuEditor> _) => { });

        // Az akció-sáv egységes megjelenése miatt a Törlés gomb elmúlt napnál is megjelenik, csak
        // letiltva — nem tűnik el, ahogy korábban.
        var deleteButtons = cut.FindAll("button[title='Nap menüjének törlése']");
        Assert.Equal(5, deleteButtons.Count);
        Assert.All(deleteButtons, b => Assert.True(b.HasAttribute("disabled")));
    }

    [Fact]
    public void Future_days_with_no_menu_show_a_disabled_delete_button()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        var mediator = BaseMediator(FuturePeriod());
        mediator.Register<GetPeriodMenuQuery, Result<IReadOnlyList<DailyMenuDto>>>(_ => Result.Success<IReadOnlyList<DailyMenuDto>>([]));
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<DailyMenuEditor>((ComponentParameterCollectionBuilder<DailyMenuEditor> _) => { });

        // Itt a letiltás oka nem az elmúlt nap, hanem hogy nincs még menü — de a gomb ugyanúgy
        // mindig megjelenik, csak letiltva.
        var deleteButtons = cut.FindAll("button[title='Nap menüjének törlése']");
        Assert.Equal(5, deleteButtons.Count);
        Assert.All(deleteButtons, b => Assert.True(b.HasAttribute("disabled")));
    }

    [Fact]
    public void Past_days_render_directly_in_the_calendar_grid_without_a_collapsible_section()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        var mediator = BaseMediator(PastPeriod());
        mediator.Register<GetPeriodMenuQuery, Result<IReadOnlyList<DailyMenuDto>>>(_ => Result.Success<IReadOnlyList<DailyMenuDto>>([]));
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<DailyMenuEditor>((ComponentParameterCollectionBuilder<DailyMenuEditor> _) => { });

        // A naptár-rács maga már elég tömör ahhoz, hogy elmúlt napokat se kelljen összecsukni — minden
        // nap dátuma közvetlenül látszik, nincs "Elmúlt napok (N)" gyűjtő szekció többé.
        var hu = System.Globalization.CultureInfo.GetCultureInfo("hu-HU");
        Assert.DoesNotContain("Elmúlt napok (", cut.Markup);
        Assert.Contains(PastWeekStart.ToString("MM.dd.", hu), cut.Markup);
        Assert.Contains(PastWeekEnd.ToString("MM.dd.", hu), cut.Markup);
    }

    [Fact]
    public void A_period_that_does_not_start_on_monday_leaves_the_earlier_weekday_columns_empty()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        // A hét szerdán kezdődik (hétfő, kedd nincs az időszakban) — a rács első heti sorában ennek a két
        // oszlopnak üresen kell maradnia, hogy a hétfő/kedd/szerda/csütörtök/péntek oszlopfelosztás stabil legyen.
        var midWeekStart = FutureWeekStart.AddDays(2);
        var period = new OrderingPeriodDto(1, "Részhét", midWeekStart, FutureWeekEnd, midWeekStart.AddDays(-10).ToDateTime(new TimeOnly(10, 0)), true, false);
        var mediator = BaseMediator(period);
        mediator.Register<GetPeriodMenuQuery, Result<IReadOnlyList<DailyMenuDto>>>(_ => Result.Success<IReadOnlyList<DailyMenuDto>>([]));
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<DailyMenuEditor>((ComponentParameterCollectionBuilder<DailyMenuEditor> _) => { });

        // Csak 3 munkanap (szerda-péntek) tartozik az időszakhoz, tehát csak 3 "Nincs még menü" napi cella van,
        // a fennmaradó 2 a heti sorban üres cella dátum/hétnap felirat nélkül.
        var noMenuCount = cut.Markup.Split("Nincs még menü erre a napra.", StringSplitOptions.None).Length - 1;
        Assert.Equal(3, noMenuCount);
        Assert.Equal(2, cut.FindAll(".menu-calendar__cell--empty").Count);
    }

    [Fact]
    public void A_period_starting_on_a_weekend_does_not_render_a_fully_empty_leading_week()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        // Az időszak szombaton kezdődik — a hét hétfője (MondayOf) egy olyan hétre esik, amelynek
        // egyetlen napja sincs még az időszakban (az első valódi munkanap a rákövetkező hétfő), ez a
        // vezető hét korábban csupa üres cellaként jelent meg, most ki kell maradnia a rácsból.
        var weekendStart = FutureWeekStart.AddDays(-2); // szombat
        var period = new OrderingPeriodDto(1, "Hétvégén kezdődő", weekendStart, FutureWeekEnd, weekendStart.AddDays(-10).ToDateTime(new TimeOnly(10, 0)), true, false);
        var mediator = BaseMediator(period);
        mediator.Register<GetPeriodMenuQuery, Result<IReadOnlyList<DailyMenuDto>>>(_ => Result.Success<IReadOnlyList<DailyMenuDto>>([]));
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<DailyMenuEditor>((ComponentParameterCollectionBuilder<DailyMenuEditor> _) => { });

        Assert.Equal(5, cut.FindAll(".menu-calendar__cell").Count);
        Assert.Empty(cut.FindAll(".menu-calendar__cell--empty"));
    }

    [Fact]
    public void Future_days_show_no_not_editable_hint()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        var mediator = BaseMediator(FuturePeriod());
        mediator.Register<GetPeriodMenuQuery, Result<IReadOnlyList<DailyMenuDto>>>(_ => Result.Success<IReadOnlyList<DailyMenuDto>>([]));
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<DailyMenuEditor>((ComponentParameterCollectionBuilder<DailyMenuEditor> _) => { });

        Assert.Empty(cut.FindAll("button[title='Elmúlt, csak megtekinthető']"));
    }

    [Fact]
    public async Task Clicking_edit_opens_the_edit_dialog_and_a_successful_save_refreshes_the_row()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        var mediator = BaseMediator(FuturePeriod());
        mediator.Register<GetPeriodMenuQuery, Result<IReadOnlyList<DailyMenuDto>>>(_ => Result.Success<IReadOnlyList<DailyMenuDto>>([]));
        mediator.Register<GetMenuDishSuggestionsQuery, MenuDishSuggestionsDto>(_ => new MenuDishSuggestionsDto([], []));
        mediator.Register<GetDailyMenuQuery, DailyMenuDto?>(_ => new DailyMenuDto(
            FutureWeekStart, IsPublished: false, Note: null, Variants: [new MenuVariantDto("A", "Rántott hús", null, 0)]));
        mediator.Register<UpsertDailyMenuCommand, Result<int>>(_ => Result.Success(1));
        Services.AddSingleton<IMediator>(mediator);

        var editorCut = Render<DailyMenuEditor>((ComponentParameterCollectionBuilder<DailyMenuEditor> _) => { });
        var providerCut = Render<MudDialogProvider>((ComponentParameterCollectionBuilder<MudDialogProvider> _) => { });

        var editButton = editorCut.FindAll("button[title='Szerkesztés']").First();
        await editorCut.InvokeAsync(() => editButton.Click());

        Assert.Contains("menüjének szerkesztése", providerCut.Markup);

        var saveButton = providerCut.FindAll("button").First(b => b.TextContent.Contains("Mentés"));
        await providerCut.InvokeAsync(() => saveButton.Click());

        // A dialógus sikeres mentés után bezárul, a napi kártya pedig a szerverről frissített (immár menüs)
        // adatot mutatja — enélkül a régi "Nincs még menü erre a napra." felirat maradna látható.
        Assert.Contains("Rántott hús", editorCut.Markup);
    }

    [Fact]
    public async Task Deleting_a_day_opens_the_confirm_dialog_and_sends_the_delete_command()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(3, "Admin Teszt", isAdmin: true));
        var mediator = BaseMediator(FuturePeriod());
        mediator.Register<GetPeriodMenuQuery, Result<IReadOnlyList<DailyMenuDto>>>(_ => Result.Success<IReadOnlyList<DailyMenuDto>>(
            [new DailyMenuDto(FutureWeekStart, IsPublished: true, Note: null, Variants: [new MenuVariantDto("A", "Rántott hús", null, 0)])]));
        DeleteDailyMenuCommand? sentCommand = null;
        mediator.Register<DeleteDailyMenuCommand, Result>(cmd =>
        {
            sentCommand = cmd;
            return Result.Success();
        });
        mediator.Register<GetDailyMenuQuery, DailyMenuDto?>((Func<GetDailyMenuQuery, DailyMenuDto?>)(_ => null));
        Services.AddSingleton<IMediator>(mediator);

        var editorCut = Render<DailyMenuEditor>((ComponentParameterCollectionBuilder<DailyMenuEditor> _) => { });
        var providerCut = Render<MudDialogProvider>((ComponentParameterCollectionBuilder<MudDialogProvider> _) => { });

        var deleteDayButton = editorCut.FindAll("button[title='Nap menüjének törlése']").First();
        await editorCut.InvokeAsync(() => deleteDayButton.Click());

        Assert.Contains("Nap menüjének törlése", providerCut.Markup);
        var confirmButton = providerCut.FindAll("button").First(b => b.TextContent.Contains("Törlés"));
        await providerCut.InvokeAsync(() => confirmButton.Click());

        Assert.NotNull(sentCommand);
        Assert.Equal(FutureWeekStart, sentCommand!.Date);
        Assert.Equal(3, sentCommand.PerformedByUserId);
    }

    [Fact]
    public void View_mode_shows_allergens_as_a_compact_number_list_with_a_full_name_tooltip()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        var mediator = BaseMediator(PastPeriod());
        mediator.Register<GetPeriodMenuQuery, Result<IReadOnlyList<DailyMenuDto>>>(_ => Result.Success<IReadOnlyList<DailyMenuDto>>(
            [new DailyMenuDto(PastWeekStart, true, null, [new MenuVariantDto("A", "Gulyásleves", "Rántott hús", 0, SoupAllergens: "1,9", MainCourseAllergens: "3")])]));
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<DailyMenuEditor>((ComponentParameterCollectionBuilder<DailyMenuEditor> _) => { });

        // A név mellett zárójelben a tömör szám-forma látszik statikusan; a teljes "szám – név" pár egy
        // info-ikonra tett MudTooltip-ben utazik. A MudTooltip csak hoverelésre rendereli a szövegét a
        // DOM-ba (MudPopover-portál), ezért ezt nem a markupból, hanem a komponensnek átadott Text
        // paraméterből ellenőrizzük.
        Assert.Contains("Gulyásleves", cut.Markup);
        Assert.Contains("(1, 9)", cut.Markup);
        Assert.Contains("Rántott hús", cut.Markup);
        Assert.Contains("(3)", cut.Markup);

        var tooltipTexts = cut.FindComponents<MudTooltip>().Select(t => t.Instance.Text).ToList();
        Assert.Contains("1 – Glutén, 9 – Zeller", tooltipTexts);
        Assert.Contains("3 – Tojás", tooltipTexts);
    }

    [Fact]
    public void View_mode_does_not_separate_variants_with_a_divider()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        var mediator = BaseMediator(PastPeriod());
        mediator.Register<GetPeriodMenuQuery, Result<IReadOnlyList<DailyMenuDto>>>(_ => Result.Success<IReadOnlyList<DailyMenuDto>>(
            [new DailyMenuDto(PastWeekStart, true, null,
            [
                new MenuVariantDto("A", "Gulyásleves", "Rántott hús", 0),
                new MenuVariantDto("B", "Húsleves", "Sertésborda", 1),
                new MenuVariantDto("C", "Bableves", "Csirkepaprikás", 2),
            ])]));
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<DailyMenuEditor>((ComponentParameterCollectionBuilder<DailyMenuEditor> _) => { });

        // Eltérő hosszúságú variáns-nevek mellett a divider inkább zavart, mint segített — nincs
        // elválasztó a variánsok között.
        Assert.Empty(cut.FindAll("hr.mud-divider"));
    }
}
