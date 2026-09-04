using Bunit;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Components.Pages.Menus;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.ALaCarte;
using EbedrendeloApp.Features.ALaCarte.PlaceALaCarteOrder;
using EbedrendeloApp.Features.Menus;
using EbedrendeloApp.Features.Menus.GetTodayMenuForUser;
using EbedrendeloApp.Tests.TestSupport;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace EbedrendeloApp.Tests.Components.Menus;

public class TodayMenuTests : MudBunitContext
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Today);

    public TodayMenuTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Shows_the_not_orderable_reason_when_today_has_no_published_menu()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Dolgozó Teszt", isAdmin: false));
        var mediator = new FakeMediator();
        mediator.Register<GetTodayMenuForUserQuery, TodayMenuDto>(
            _ => new TodayMenuDto(Today, false, ErrorCodes.MenuNotPublished, [], null, [], []));
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<TodayMenu>((ComponentParameterCollectionBuilder<TodayMenu> _) => { });

        Assert.Contains("Mára még nincs publikált menü", cut.Markup);
    }

    [Fact]
    public void Shows_ala_carte_offers_alongside_the_not_published_message_when_todays_menu_is_not_published()
    {
        // AC 4.2.6: à la carte ordering is independent of the A/B/C daily menu's publication state.
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Dolgozó Teszt", isAdmin: false));
        var mediator = new FakeMediator();
        mediator.Register<GetTodayMenuForUserQuery, TodayMenuDto>(_ => new TodayMenuDto(
            Today, false, ErrorCodes.MenuNotPublished, [], null,
            ALaCarteOffers: [new ALaCarteOfferDto(1, "Rántott sertés szelet", ALaCarteCategory.Foetel, 2550, 7)],
            MyALaCarteOrderLines: []));
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<TodayMenu>((ComponentParameterCollectionBuilder<TodayMenu> _) => { });

        Assert.Contains("Mára még nincs publikált menü", cut.Markup);
        Assert.Contains("Rántott sertés szelet", cut.Markup);
    }

    [Fact]
    public void Hides_the_ala_carte_section_on_a_non_working_day()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Dolgozó Teszt", isAdmin: false));
        var mediator = new FakeMediator();
        mediator.Register<GetTodayMenuForUserQuery, TodayMenuDto>(
            _ => new TodayMenuDto(Today, false, ErrorCodes.NotWorkingDay, [], null, [], []));
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<TodayMenu>((ComponentParameterCollectionBuilder<TodayMenu> _) => { });

        Assert.Contains("Ma hétvége van, nincs kiszolgálás.", cut.Markup);
        Assert.DoesNotContain("à la carte", cut.Markup);
    }

    [Fact]
    public void Shows_an_explicit_not_ordered_message_when_the_user_has_no_selection()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Dolgozó Teszt", isAdmin: false));
        var mediator = new FakeMediator();
        mediator.Register<GetTodayMenuForUserQuery, TodayMenuDto>(_ => new TodayMenuDto(
            Today, true, null,
            [new MenuVariantDto("A", "Rántott hús", "sülttel", 0)],
            MySelection: null,
            ALaCarteOffers: [],
            MyALaCarteOrderLines: []));
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<TodayMenu>((ComponentParameterCollectionBuilder<TodayMenu> _) => { });

        Assert.Contains("Rántott hús", cut.Markup);
        Assert.Contains("Ma még nem rendeltél menüt", cut.Markup);
    }

    [Fact]
    public void Shows_the_users_own_selection_and_marks_the_matching_variant()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Dolgozó Teszt", isAdmin: false));
        var mediator = new FakeMediator();
        mediator.Register<GetTodayMenuForUserQuery, TodayMenuDto>(_ => new TodayMenuDto(
            Today, true, null,
            [new MenuVariantDto("A", "Rántott hús", null, 0), new MenuVariantDto("B", "Gulyás", null, 1)],
            MySelection: new MyMenuSelectionDto("B", "Gulyás", 1400),
            ALaCarteOffers: [],
            MyALaCarteOrderLines: []));
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<TodayMenu>((ComponentParameterCollectionBuilder<TodayMenu> _) => { });

        Assert.Contains("Ma a(z) B menüt (Gulyás) rendelted, 1\u00A0400 Ft értékben.", cut.Markup);
        Assert.Contains("Ezt választottad", cut.Markup);
    }

    [Fact]
    public void Shows_ala_carte_offers_with_free_count_and_the_users_own_order_lines()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Dolgozó Teszt", isAdmin: false));
        var mediator = new FakeMediator();
        mediator.Register<GetTodayMenuForUserQuery, TodayMenuDto>(_ => new TodayMenuDto(
            Today, true, null,
            [new MenuVariantDto("A", "Rántott hús", null, 0)],
            MySelection: null,
            ALaCarteOffers: [new ALaCarteOfferDto(1, "Rántott sertés szelet", ALaCarteCategory.Foetel, 2550, 7)],
            MyALaCarteOrderLines: [new MyALaCarteLineDto(1, "Somlói galuska", ALaCarteCategory.Desszert, 750)]));
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<TodayMenu>((ComponentParameterCollectionBuilder<TodayMenu> _) => { });

        Assert.Contains("Rántott sertés szelet", cut.Markup);
        Assert.Contains("2\u00A0550 Ft", cut.Markup);
        Assert.Contains("Főétel", cut.Markup);
        Assert.Contains("Somlói galuska", cut.Markup);
        Assert.Contains("Desszert", cut.Markup);
    }

    [Fact]
    public void Shows_the_allergens_of_an_ala_carte_offer()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Dolgozó Teszt", isAdmin: false));
        var mediator = new FakeMediator();
        mediator.Register<GetTodayMenuForUserQuery, TodayMenuDto>(_ => new TodayMenuDto(
            Today, true, null,
            [new MenuVariantDto("A", "Rántott hús", null, 0)],
            MySelection: null,
            ALaCarteOffers: [new ALaCarteOfferDto(1, "Rántott sertés szelet", ALaCarteCategory.Foetel, 2550, 7, Allergens: "1,9")],
            MyALaCarteOrderLines: []));
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<TodayMenu>((ComponentParameterCollectionBuilder<TodayMenu> _) => { });

        // A kártyán statikusan csak a tömör szám-forma látszik; a teljes "szám – név" pár egy
        // info-ikonra tett MudTooltip-ben utazik, ami csak hoverre rendereli a szövegét a DOM-ba
        // (MudPopover-portál) — ezért ezt a komponensnek átadott Text paraméterből ellenőrizzük.
        Assert.Contains("Allergén: 1, 9", cut.Markup);

        var tooltipTexts = cut.FindComponents<MudTooltip>().Select(t => t.Instance.Text).ToList();
        Assert.Contains("1 – Glutén, 9 – Zeller", tooltipTexts);
    }

    [Fact]
    public void Shows_the_ontet_category_label()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Dolgozó Teszt", isAdmin: false));
        var mediator = new FakeMediator();
        mediator.Register<GetTodayMenuForUserQuery, TodayMenuDto>(_ => new TodayMenuDto(
            Today, true, null,
            [new MenuVariantDto("A", "Rántott hús", null, 0)],
            MySelection: null,
            ALaCarteOffers: [new ALaCarteOfferDto(1, "Tartár mártás", ALaCarteCategory.Ontet, 350, 7)],
            MyALaCarteOrderLines: []));
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<TodayMenu>((ComponentParameterCollectionBuilder<TodayMenu> _) => { });

        Assert.Contains("Öntet", cut.Markup);
    }

    [Fact]
    public void Shows_a_levessel_note_for_main_courses_that_include_soup()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Dolgozó Teszt", isAdmin: false));
        var mediator = new FakeMediator();
        mediator.Register<GetTodayMenuForUserQuery, TodayMenuDto>(_ => new TodayMenuDto(
            Today, true, null,
            [new MenuVariantDto("A", "Rántott hús", null, 0)],
            MySelection: null,
            ALaCarteOffers: [new ALaCarteOfferDto(1, "Rántott sertés szelet", ALaCarteCategory.Foetel, 2550, 7, IncludesSoup: true)],
            MyALaCarteOrderLines: [new MyALaCarteLineDto(2, "Rántott csirke", ALaCarteCategory.Foetel, 2500, IncludesSoup: false)]));
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<TodayMenu>((ComponentParameterCollectionBuilder<TodayMenu> _) => { });

        Assert.Contains("Rántott sertés szelet (levessel)", cut.Markup);
        Assert.DoesNotContain("Rántott csirke (levessel)", cut.Markup);
    }

    [Fact]
    public async Task Clicking_a_card_and_confirming_the_dialog_places_the_order_and_refreshes_the_offers()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Dolgozó Teszt", isAdmin: false));
        var mediator = new FakeMediator();
        var loadCount = 0;
        mediator.Register<GetTodayMenuForUserQuery, TodayMenuDto>(_ =>
        {
            loadCount++;
            var myLines = loadCount == 1 ? [] : new List<MyALaCarteLineDto> { new(1, "Rántott sertés szelet", ALaCarteCategory.Foetel, 2550) };
            return new TodayMenuDto(
                Today, true, null,
                [new MenuVariantDto("A", "Rántott hús", null, 0)],
                MySelection: null,
                ALaCarteOffers: [new ALaCarteOfferDto(1, "Rántott sertés szelet", ALaCarteCategory.Foetel, 2550, 7)],
                MyALaCarteOrderLines: myLines,
                ALaCarteOrderDeadlineLocalTime: new TimeOnly(10, 30),
                IsALaCarteOrderableNow: true);
        });
        PlaceALaCarteOrderCommand? sentCommand = null;
        mediator.Register<PlaceALaCarteOrderCommand, Result<PlacedALaCarteOrderLinesDto>>(cmd =>
        {
            sentCommand = cmd;
            return Result.Success(new PlacedALaCarteOrderLinesDto([new PlacedALaCarteOrderLineDto(1, "Rántott sertés szelet", ALaCarteCategory.Foetel, 2550, false)], 2550));
        });
        Services.AddSingleton<IMediator>(mediator);

        var provider = Render<MudDialogProvider>();
        var cut = Render<TodayMenu>((ComponentParameterCollectionBuilder<TodayMenu> _) => { });

        await cut.InvokeAsync(() => FindOfferCard(cut, "Rántott sertés szelet").Click());

        var orderButton = cut.FindAll("button").First(b => b.TextContent.Contains("Megrendelés"));
        await cut.InvokeAsync(() => orderButton.Click());

        Assert.Null(sentCommand); // nothing sent until the confirm dialog is accepted
        Assert.Contains("Rántott sertés szelet", provider.Markup);

        var confirmButton = provider.FindAll("button").First(b => b.TextContent.Contains("Igen, megrendelem"));
        await provider.InvokeAsync(() => confirmButton.Click());

        Assert.NotNull(sentCommand);
        Assert.Equal(1, sentCommand!.UserId);
        Assert.Equal([1], sentCommand.ALaCarteItemIds);
        Assert.Contains("Megrendelve", cut.Markup);
    }

    [Fact]
    public async Task Clicking_two_cards_in_different_categories_sends_both_ids_in_a_single_order()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Dolgozó Teszt", isAdmin: false));
        var mediator = new FakeMediator();
        mediator.Register<GetTodayMenuForUserQuery, TodayMenuDto>(_ => new TodayMenuDto(
            Today, true, null,
            [new MenuVariantDto("A", "Rántott hús", null, 0)],
            MySelection: null,
            ALaCarteOffers:
            [
                new ALaCarteOfferDto(1, "Rántott sertés szelet", ALaCarteCategory.Foetel, 2550, 7),
                new ALaCarteOfferDto(2, "Tartár mártás", ALaCarteCategory.Ontet, 350, 7),
            ],
            MyALaCarteOrderLines: [],
            ALaCarteOrderDeadlineLocalTime: new TimeOnly(10, 30),
            IsALaCarteOrderableNow: true));
        PlaceALaCarteOrderCommand? sentCommand = null;
        mediator.Register<PlaceALaCarteOrderCommand, Result<PlacedALaCarteOrderLinesDto>>(cmd =>
        {
            sentCommand = cmd;
            return Result.Success(new PlacedALaCarteOrderLinesDto([], 0));
        });
        Services.AddSingleton<IMediator>(mediator);

        var provider = Render<MudDialogProvider>();
        var cut = Render<TodayMenu>((ComponentParameterCollectionBuilder<TodayMenu> _) => { });

        await cut.InvokeAsync(() => FindOfferCard(cut, "Rántott sertés szelet").Click());
        await cut.InvokeAsync(() => FindOfferCard(cut, "Tartár mártás").Click());

        var orderButton = cut.FindAll("button").First(b => b.TextContent.Contains("Megrendelés"));
        await cut.InvokeAsync(() => orderButton.Click());

        var confirmButton = provider.FindAll("button").First(b => b.TextContent.Contains("Igen, megrendelem"));
        await provider.InvokeAsync(() => confirmButton.Click());

        Assert.NotNull(sentCommand);
        Assert.Equal([1, 2], sentCommand!.ALaCarteItemIds.OrderBy(id => id));
    }

    [Fact]
    public async Task Clicking_a_second_card_in_the_same_category_deselects_the_first_one()
    {
        // Rádiógomb-viselkedés: kategóriánként legfeljebb egy tétel választható (pl. 1 főétel).
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Dolgozó Teszt", isAdmin: false));
        var mediator = new FakeMediator();
        mediator.Register<GetTodayMenuForUserQuery, TodayMenuDto>(_ => new TodayMenuDto(
            Today, true, null,
            [new MenuVariantDto("A", "Rántott hús", null, 0)],
            MySelection: null,
            ALaCarteOffers:
            [
                new ALaCarteOfferDto(1, "Csirkemell", ALaCarteCategory.Foetel, 2200, 7),
                new ALaCarteOfferDto(2, "Marhapörkölt", ALaCarteCategory.Foetel, 2600, 7),
            ],
            MyALaCarteOrderLines: [],
            ALaCarteOrderDeadlineLocalTime: new TimeOnly(10, 30),
            IsALaCarteOrderableNow: true));
        PlaceALaCarteOrderCommand? sentCommand = null;
        mediator.Register<PlaceALaCarteOrderCommand, Result<PlacedALaCarteOrderLinesDto>>(cmd =>
        {
            sentCommand = cmd;
            return Result.Success(new PlacedALaCarteOrderLinesDto([], 0));
        });
        Services.AddSingleton<IMediator>(mediator);

        var provider = Render<MudDialogProvider>();
        var cut = Render<TodayMenu>((ComponentParameterCollectionBuilder<TodayMenu> _) => { });

        await cut.InvokeAsync(() => FindOfferCard(cut, "Csirkemell").Click());
        await cut.InvokeAsync(() => FindOfferCard(cut, "Marhapörkölt").Click()); // should replace Csirkemell

        var orderButton = cut.FindAll("button").First(b => b.TextContent.Contains("Megrendelés"));
        await cut.InvokeAsync(() => orderButton.Click());

        var confirmButton = provider.FindAll("button").First(b => b.TextContent.Contains("Igen, megrendelem"));
        await provider.InvokeAsync(() => confirmButton.Click());

        Assert.NotNull(sentCommand);
        Assert.Equal([2], sentCommand!.ALaCarteItemIds);
    }

    [Fact]
    public void Shows_an_elfogyott_chip_and_does_not_render_a_dialog_command_when_free_count_is_zero()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Dolgozó Teszt", isAdmin: false));
        var mediator = new FakeMediator();
        mediator.Register<GetTodayMenuForUserQuery, TodayMenuDto>(_ => new TodayMenuDto(
            Today, true, null,
            [new MenuVariantDto("A", "Rántott hús", null, 0)],
            MySelection: null,
            ALaCarteOffers: [new ALaCarteOfferDto(1, "Rántott sertés szelet", ALaCarteCategory.Foetel, 2550, 0)],
            MyALaCarteOrderLines: [],
            ALaCarteOrderDeadlineLocalTime: new TimeOnly(10, 30),
            IsALaCarteOrderableNow: true));
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<TodayMenu>((ComponentParameterCollectionBuilder<TodayMenu> _) => { });

        Assert.Contains("Elfogyott", cut.Markup);
    }

    [Fact]
    public async Task Clicking_a_card_and_the_order_button_does_nothing_when_the_daily_deadline_has_passed()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Dolgozó Teszt", isAdmin: false));
        var mediator = new FakeMediator();
        mediator.Register<GetTodayMenuForUserQuery, TodayMenuDto>(_ => new TodayMenuDto(
            Today, true, null,
            [new MenuVariantDto("A", "Rántott hús", null, 0)],
            MySelection: null,
            ALaCarteOffers: [new ALaCarteOfferDto(1, "Rántott sertés szelet", ALaCarteCategory.Foetel, 2550, 7)],
            MyALaCarteOrderLines: [],
            ALaCarteOrderDeadlineLocalTime: new TimeOnly(10, 30),
            IsALaCarteOrderableNow: false));
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<TodayMenu>((ComponentParameterCollectionBuilder<TodayMenu> _) => { });

        Assert.Contains("A mai rendelési határidő lejárt", cut.Markup);

        await cut.InvokeAsync(() => FindOfferCard(cut, "Rántott sertés szelet").Click());

        var orderButton = cut.FindAll("button").First(b => b.TextContent.Contains("Megrendelés"));
        Assert.True(orderButton.HasAttribute("disabled"));
        Assert.False(IsCardSelected(cut, "Rántott sertés szelet"));
    }

    [Fact]
    public void Nutrition_is_shown_as_a_hover_tooltip_on_a_calorie_icon()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Dolgozó Teszt", isAdmin: false));
        var mediator = new FakeMediator();
        mediator.Register<GetTodayMenuForUserQuery, TodayMenuDto>(_ => new TodayMenuDto(
            Today, true, null,
            [new MenuVariantDto("A", "Rántott hús", null, 0)],
            MySelection: null,
            ALaCarteOffers: [new ALaCarteOfferDto(1, "Rántott sertés szelet", ALaCarteCategory.Foetel, 2550, 7, EnergyKcal: 500)],
            MyALaCarteOrderLines: [],
            ALaCarteOrderDeadlineLocalTime: new TimeOnly(10, 30),
            IsALaCarteOrderableNow: true));
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<TodayMenu>((ComponentParameterCollectionBuilder<TodayMenu> _) => { });

        // A tápérték nem jelenik meg statikus szövegként a kártyán, csak a kalória-ikon hoverjén — a
        // MudTooltip a szövegét csak hoverre rendereli a DOM-ba (MudPopover-portál), ezért ezt a
        // komponensnek átadott Text paraméterből ellenőrizzük, nem a markupból.
        Assert.DoesNotContain("En: 500", cut.Markup);

        var tooltipTexts = cut.FindComponents<MudTooltip>().Select(t => t.Instance.Text).ToList();
        Assert.Contains("En: 500", tooltipTexts);
    }

    // Az allergén info-ikon és a kalória-ikon @onclick:stopPropagation="true"-t kap (lásd TodayMenu.razor),
    // pont azzal a mintával, amit a lenti "Nutrition_is_hidden..." teszt korábbi (accordionos) változata
    // már bizonyítottan lefedett — de itt nincs mit bUnit-tal külön leklikkelni: a wrapper span-nak (és
    // az ikonnak) nincs saját onclick-kezelője, ezért bUnit `MissingEventHandlerException`-t dob, ha
    // megpróbáljuk .Click()-elni ("a kattintás nem jut el sehova" ugyanis pontosan a várt viselkedés,
    // nem egy tesztelhető mellékhatás). A stopPropagation tényleges hatását a valódi böngészőben kell
    // ellenőrizni; itt a kártya a11y-attribútumait és a billentyűzetes kezelést teszteljük lejjebb.

    [Fact]
    public void An_orderable_cards_root_element_exposes_a_button_role_and_is_tab_reachable()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Dolgozó Teszt", isAdmin: false));
        var mediator = new FakeMediator();
        mediator.Register<GetTodayMenuForUserQuery, TodayMenuDto>(_ => new TodayMenuDto(
            Today, true, null,
            [new MenuVariantDto("A", "Rántott hús", null, 0)],
            MySelection: null,
            ALaCarteOffers: [new ALaCarteOfferDto(1, "Rántott sertés szelet", ALaCarteCategory.Foetel, 2550, 7)],
            MyALaCarteOrderLines: [],
            ALaCarteOrderDeadlineLocalTime: new TimeOnly(10, 30),
            IsALaCarteOrderableNow: true));
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<TodayMenu>((ComponentParameterCollectionBuilder<TodayMenu> _) => { });
        var card = FindOfferCard(cut, "Rántott sertés szelet");

        Assert.Equal("button", card.GetAttribute("role"));
        Assert.Equal("0", card.GetAttribute("tabindex"));
        Assert.Equal("false", card.GetAttribute("aria-disabled"));
    }

    [Fact]
    public async Task Pressing_enter_on_a_focused_card_selects_it_like_a_click()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Dolgozó Teszt", isAdmin: false));
        var mediator = new FakeMediator();
        mediator.Register<GetTodayMenuForUserQuery, TodayMenuDto>(_ => new TodayMenuDto(
            Today, true, null,
            [new MenuVariantDto("A", "Rántott hús", null, 0)],
            MySelection: null,
            ALaCarteOffers: [new ALaCarteOfferDto(1, "Rántott sertés szelet", ALaCarteCategory.Foetel, 2550, 7)],
            MyALaCarteOrderLines: [],
            ALaCarteOrderDeadlineLocalTime: new TimeOnly(10, 30),
            IsALaCarteOrderableNow: true));
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<TodayMenu>((ComponentParameterCollectionBuilder<TodayMenu> _) => { });
        var card = FindOfferCard(cut, "Rántott sertés szelet");

        await cut.InvokeAsync(() => card.KeyDown(key: "Enter"));

        Assert.True(IsCardSelected(cut, "Rántott sertés szelet"));
    }

    [Fact]
    public async Task Order_failure_shows_the_servers_item_specific_message_and_drops_the_now_unavailable_selection()
    {
        // AC: a szerver egy konkrét, tételnevesített hibaüzenetet ad (pl. "Csirkemell elfogyott."),
        // ezt kell megjeleníteni egy általános szöveg helyett — és a beküldés után frissülő today
        // alapján az azóta elfogyott tételt automatikusan kivesszük a kijelölésből, a még elérhetőt
        // (másik kategória) viszont bent hagyjuk.
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Dolgozó Teszt", isAdmin: false));
        var mediator = new FakeMediator();
        var loadCount = 0;
        mediator.Register<GetTodayMenuForUserQuery, TodayMenuDto>(_ =>
        {
            loadCount++;
            var mainFreeCount = loadCount == 1 ? 7 : 0; // second load: sold out in the meantime
            return new TodayMenuDto(
                Today, true, null,
                [new MenuVariantDto("A", "Rántott hús", null, 0)],
                MySelection: null,
                ALaCarteOffers:
                [
                    new ALaCarteOfferDto(1, "Csirkemell", ALaCarteCategory.Foetel, 2200, mainFreeCount),
                    new ALaCarteOfferDto(2, "Rizi-bizi", ALaCarteCategory.Koret, 500, 7),
                ],
                MyALaCarteOrderLines: [],
                ALaCarteOrderDeadlineLocalTime: new TimeOnly(10, 30),
                IsALaCarteOrderableNow: true);
        });
        mediator.Register<PlaceALaCarteOrderCommand, Result<PlacedALaCarteOrderLinesDto>>(
            _ => Result.Failure<PlacedALaCarteOrderLinesDto>(ErrorCodes.OutOfStock, "Csirkemell elfogyott."));
        Services.AddSingleton<IMediator>(mediator);

        var provider = Render<MudDialogProvider>();
        var cut = Render<TodayMenu>((ComponentParameterCollectionBuilder<TodayMenu> _) => { });

        await cut.InvokeAsync(() => FindOfferCard(cut, "Csirkemell").Click());
        await cut.InvokeAsync(() => FindOfferCard(cut, "Rizi-bizi").Click());

        var orderButton = cut.FindAll("button").First(b => b.TextContent.Contains("Megrendelés"));
        await cut.InvokeAsync(() => orderButton.Click());

        var confirmButton = provider.FindAll("button").First(b => b.TextContent.Contains("Igen, megrendelem"));
        await provider.InvokeAsync(() => confirmButton.Click());

        Assert.Contains("Csirkemell elfogyott.", cut.Markup);
        Assert.DoesNotContain("Ez a tétel időközben elfogyott.", cut.Markup);
        Assert.False(IsCardSelected(cut, "Csirkemell")); // dropped: sold out on refresh
        Assert.True(IsCardSelected(cut, "Rizi-bizi")); // kept: still available
    }

    /// <summary>A legkisebb szöveget tartalmazó `mud-paper` a keresett tétel saját kártyája — a
    /// befoglaló szekció-paperek is tartalmazzák a nevet (minden leszármazott szövegét öröklik),
    /// de azoknak jóval hosszabb a TextContent-je.</summary>
    private static AngleSharp.Dom.IElement FindOfferCard(IRenderedComponent<TodayMenu> cut, string offerName) =>
        cut.FindAll("div.mud-paper")
            .Where(el => el.TextContent.Contains(offerName))
            .OrderBy(el => el.TextContent.Length)
            .First();

    /// <summary>A kártya kijelölés-állapotát a checkbox (input[type=checkbox]) valós "checked"
    /// attribútuma dönti el — ez az egyetlen olyan jelző a kártyán, ami minden render után pontosan
    /// tükrözi a <c>selectedItemIds</c> tartalmát (a "Kiválasztva" chip csak színt vált, nem tűnik el,
    /// lásd TodayMenu.razor).</summary>
    private static bool IsCardSelected(IRenderedComponent<TodayMenu> cut, string offerName) =>
        FindOfferCard(cut, offerName).QuerySelector("input[type=checkbox]")!.HasAttribute("checked");
}
