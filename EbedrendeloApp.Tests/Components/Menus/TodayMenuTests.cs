using Bunit;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Components.Pages.Menus;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Menus;
using EbedrendeloApp.Features.Menus.GetTodayMenuForUser;
using EbedrendeloApp.Tests.TestSupport;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
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

        Assert.Contains("Ma a(z) B menüt (Gulyás) rendelted, 1400 Ft értékben.", cut.Markup);
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
            ALaCarteOffers: [new ALaCarteOfferDto(1, "Húsleves", ALaCarteCategory.Leves, 650, 7)],
            MyALaCarteOrderLines: [new MyALaCarteLineDto("Somlói galuska", ALaCarteCategory.Desszert, 750)]));
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<TodayMenu>((ComponentParameterCollectionBuilder<TodayMenu> _) => { });

        Assert.Contains("Húsleves", cut.Markup);
        Assert.Contains("650 Ft", cut.Markup);
        Assert.Contains("Leves", cut.Markup);
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
            ALaCarteOffers: [new ALaCarteOfferDto(1, "Húsleves", ALaCarteCategory.Leves, 650, 7, Allergens: "1,9")],
            MyALaCarteOrderLines: []));
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<TodayMenu>((ComponentParameterCollectionBuilder<TodayMenu> _) => { });

        Assert.Contains("1 – Glutén", cut.Markup);
        Assert.Contains("9 – Zeller", cut.Markup);
    }
}
