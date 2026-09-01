using Bunit;
using Bunit.TestDoubles;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Components.Pages.ALaCarte;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.ALaCarte;
using EbedrendeloApp.Features.ALaCarte.GetALaCarteItems;
using EbedrendeloApp.Features.ALaCarte.GetDailyOffers;
using EbedrendeloApp.Features.ALaCarte.RemoveDailyOffer;
using EbedrendeloApp.Features.ALaCarte.SetDailyOffer;
using EbedrendeloApp.Tests.TestSupport;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace EbedrendeloApp.Tests.Components.ALaCarte;

public class AdminALaCarteDailyOfferTests : MudBunitContext
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Today);

    public AdminALaCarteDailyOfferTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private static ALaCarteItemDto Item(int id, string name, ALaCarteCategory category) =>
        new(id, name, category, 500, true, null, null, null, null, null, null, null, null);

    private static ALaCarteDailyOfferDto Offer(int offerId, int itemId, string name, ALaCarteCategory category, int capacity, int ordered) =>
        new(offerId, Today, itemId, name, category, 500, capacity, ordered, capacity - ordered);

    [Fact]
    public void Redirects_non_admin_users_to_the_today_menu_page()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(2, "Dolgozó Teszt", isAdmin: false));
        Services.AddSingleton<IMediator>(new FakeMediator());

        Render<AdminALaCarteDailyOffer>((ComponentParameterCollectionBuilder<AdminALaCarteDailyOffer> _) => { });

        var navigationManager = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/mai-menu", navigationManager.Uri);
    }

    [Fact]
    public void The_soup_select_is_only_visible_while_editing()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        var mediator = new FakeMediator();
        mediator.Register<GetALaCarteItemsQuery, IReadOnlyList<ALaCarteItemDto>>(_ => [Item(1, "Csontleves", ALaCarteCategory.Leves)]);
        mediator.Register<GetDailyOffersQuery, IReadOnlyList<ALaCarteDailyOfferDto>>(_ => []);
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<AdminALaCarteDailyOffer>((ComponentParameterCollectionBuilder<AdminALaCarteDailyOffer> _) => { });

        Assert.Empty(cut.FindComponents<MudBlazor.MudSelect<int?>>());
        Assert.Contains("Ma nincs leves beállítva.", cut.Markup);
    }

    [Fact]
    public void Selecting_a_soup_and_saving_sends_a_set_offer_command_with_zero_capacity()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        var mediator = new FakeMediator();
        mediator.Register<GetALaCarteItemsQuery, IReadOnlyList<ALaCarteItemDto>>(_ => [Item(1, "Csontleves", ALaCarteCategory.Leves)]);
        mediator.Register<GetDailyOffersQuery, IReadOnlyList<ALaCarteDailyOfferDto>>(_ => []);
        SetDailyOfferCommand? sentCommand = null;
        mediator.Register<SetDailyOfferCommand, Result<ALaCarteDailyOfferDto>>(cmd =>
        {
            sentCommand = cmd;
            return Result.Success(Offer(10, 1, "Csontleves", ALaCarteCategory.Leves, int.MaxValue, 0));
        });
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<AdminALaCarteDailyOffer>((ComponentParameterCollectionBuilder<AdminALaCarteDailyOffer> _) => { });
        ClickButtonWithText(cut, "Szerkesztés");
        var select = cut.FindComponent<MudBlazor.MudSelect<int?>>();
        cut.InvokeAsync(() => select.Instance.ValueChanged.InvokeAsync(1));
        ClickButtonWithText(cut, "Mentés");

        Assert.NotNull(sentCommand);
        Assert.Equal(1, sentCommand!.ALaCarteItemId);
        Assert.Equal(0, sentCommand.Capacity);
    }

    [Fact]
    public void Derives_the_soup_portion_count_from_the_foetel_offers_ordered_count()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        var mediator = new FakeMediator();
        mediator.Register<GetALaCarteItemsQuery, IReadOnlyList<ALaCarteItemDto>>(
            _ => [Item(2, "Rántott szelet", ALaCarteCategory.Foetel), Item(3, "Csirkemell", ALaCarteCategory.Foetel), Item(4, "Rizi-bizi", ALaCarteCategory.Koret)]);
        mediator.Register<GetDailyOffersQuery, IReadOnlyList<ALaCarteDailyOfferDto>>(_ => [
            Offer(11, 2, "Rántott szelet", ALaCarteCategory.Foetel, 10, 3),
            Offer(12, 3, "Csirkemell", ALaCarteCategory.Foetel, 10, 2),
            Offer(13, 4, "Rizi-bizi", ALaCarteCategory.Koret, 10, 7), // not a Főétel — must not count toward the soup portions
        ]);
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<AdminALaCarteDailyOffer>((ComponentParameterCollectionBuilder<AdminALaCarteDailyOffer> _) => { });

        Assert.Contains("5 levesadag ma", cut.Markup);
    }

    private static void ClickButtonWithText(IRenderedComponent<AdminALaCarteDailyOffer> cut, string text)
        => cut.InvokeAsync(() => cut.FindAll("button").First(b => b.TextContent.Contains(text)).Click());

    [Fact]
    public void A_typed_capacity_survives_a_re_render_and_is_sent_on_save()
    {
        // Regression test: rows used to be rebuilt from scratch on every render (a method called
        // directly from the MudTable's Items binding), which discarded whatever the admin had just
        // typed into the Kapacitás field before they could click Mentés.
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        var mediator = new FakeMediator();
        mediator.Register<GetALaCarteItemsQuery, IReadOnlyList<ALaCarteItemDto>>(_ => [Item(2, "Rántott szelet", ALaCarteCategory.Foetel)]);
        mediator.Register<GetDailyOffersQuery, IReadOnlyList<ALaCarteDailyOfferDto>>(_ => [Offer(11, 2, "Rántott szelet", ALaCarteCategory.Foetel, 5, 0)]);
        SetDailyOfferCommand? sentCommand = null;
        mediator.Register<SetDailyOfferCommand, Result<ALaCarteDailyOfferDto>>(cmd =>
        {
            sentCommand = cmd;
            return Result.Success(Offer(11, 2, "Rántott szelet", ALaCarteCategory.Foetel, cmd.Capacity, 0));
        });
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<AdminALaCarteDailyOffer>((ComponentParameterCollectionBuilder<AdminALaCarteDailyOffer> _) => { });
        ClickButtonWithText(cut, "Szerkesztés");

        var capacityField = cut.FindComponent<MudBlazor.MudNumericField<int>>();
        cut.InvokeAsync(() => capacityField.Instance.ValueChanged.InvokeAsync(20));
        cut.Render(); // force a re-render before saving, which previously wiped the typed value

        ClickButtonWithText(cut, "Mentés");

        Assert.NotNull(sentCommand);
        Assert.Equal(20, sentCommand!.Capacity);
    }

    [Fact]
    public void Unchanged_rows_are_not_sent_when_saving()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        var mediator = new FakeMediator();
        mediator.Register<GetALaCarteItemsQuery, IReadOnlyList<ALaCarteItemDto>>(
            _ => [Item(2, "Rántott szelet", ALaCarteCategory.Foetel), Item(3, "Csirkemell", ALaCarteCategory.Foetel)]);
        mediator.Register<GetDailyOffersQuery, IReadOnlyList<ALaCarteDailyOfferDto>>(
            _ => [Offer(11, 2, "Rántott szelet", ALaCarteCategory.Foetel, 5, 0)]);
        var sentItemIds = new List<int>();
        mediator.Register<SetDailyOfferCommand, Result<ALaCarteDailyOfferDto>>(cmd =>
        {
            sentItemIds.Add(cmd.ALaCarteItemId);
            return Result.Success(Offer(11, cmd.ALaCarteItemId, "X", ALaCarteCategory.Foetel, cmd.Capacity, 0));
        });
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<AdminALaCarteDailyOffer>((ComponentParameterCollectionBuilder<AdminALaCarteDailyOffer> _) => { });
        ClickButtonWithText(cut, "Szerkesztés");

        // Only touch the second item (Csirkemell, currently unoffered) — the first (already at 5) stays untouched.
        var capacityFields = cut.FindComponents<MudBlazor.MudNumericField<int>>();
        cut.InvokeAsync(() => capacityFields[1].Instance.ValueChanged.InvokeAsync(3));
        ClickButtonWithText(cut, "Mentés");

        Assert.Equal([3], sentItemIds);
    }

    [Fact]
    public void Megse_discards_typed_changes_without_calling_the_server()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        var mediator = new FakeMediator();
        mediator.Register<GetALaCarteItemsQuery, IReadOnlyList<ALaCarteItemDto>>(_ => [Item(2, "Rántott szelet", ALaCarteCategory.Foetel)]);
        mediator.Register<GetDailyOffersQuery, IReadOnlyList<ALaCarteDailyOfferDto>>(_ => [Offer(11, 2, "Rántott szelet", ALaCarteCategory.Foetel, 5, 0)]);
        var saveCalled = false;
        mediator.Register<SetDailyOfferCommand, Result<ALaCarteDailyOfferDto>>(cmd =>
        {
            saveCalled = true;
            return Result.Success(Offer(11, 2, "Rántott szelet", ALaCarteCategory.Foetel, cmd.Capacity, 0));
        });
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<AdminALaCarteDailyOffer>((ComponentParameterCollectionBuilder<AdminALaCarteDailyOffer> _) => { });
        ClickButtonWithText(cut, "Szerkesztés");
        var capacityField = cut.FindComponent<MudBlazor.MudNumericField<int>>();
        cut.InvokeAsync(() => capacityField.Instance.ValueChanged.InvokeAsync(99));

        ClickButtonWithText(cut, "Mégse");

        Assert.False(saveCalled);
        Assert.Contains("Szerkesztés", cut.Markup); // back to view mode
    }

    [Fact]
    public void Nullazas_zeroes_every_row_locally_without_calling_the_server()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        var mediator = new FakeMediator();
        mediator.Register<GetALaCarteItemsQuery, IReadOnlyList<ALaCarteItemDto>>(_ => [Item(2, "Rántott szelet", ALaCarteCategory.Foetel)]);
        mediator.Register<GetDailyOffersQuery, IReadOnlyList<ALaCarteDailyOfferDto>>(_ => [Offer(11, 2, "Rántott szelet", ALaCarteCategory.Foetel, 5, 0)]);
        SetDailyOfferCommand? sentCommand = null;
        mediator.Register<SetDailyOfferCommand, Result<ALaCarteDailyOfferDto>>(cmd =>
        {
            sentCommand = cmd;
            return Result.Success(Offer(11, 2, "Rántott szelet", ALaCarteCategory.Foetel, cmd.Capacity, 0));
        });
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<AdminALaCarteDailyOffer>((ComponentParameterCollectionBuilder<AdminALaCarteDailyOffer> _) => { });
        ClickButtonWithText(cut, "Szerkesztés");
        ClickButtonWithText(cut, "Nullázás");

        Assert.Null(sentCommand); // purely local — nothing sent until Mentés

        ClickButtonWithText(cut, "Mentés");

        Assert.NotNull(sentCommand);
        Assert.Equal(0, sentCommand!.Capacity);
    }

    [Fact]
    public void Stepping_the_date_navigator_reloads_offers_for_the_new_day()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        var mediator = new FakeMediator();
        mediator.Register<GetALaCarteItemsQuery, IReadOnlyList<ALaCarteItemDto>>(_ => []);
        var requestedDates = new List<DateOnly>();
        mediator.Register<GetDailyOffersQuery, IReadOnlyList<ALaCarteDailyOfferDto>>(q =>
        {
            requestedDates.Add(q.Date);
            return [];
        });
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<AdminALaCarteDailyOffer>((ComponentParameterCollectionBuilder<AdminALaCarteDailyOffer> _) => { });
        cut.Find("button[title='Következő']").Click();

        Assert.Equal([Today, Today.AddDays(1)], requestedDates);
    }

    [Fact]
    public void Shows_the_servers_error_and_stays_in_edit_mode_when_saving_a_row_fails()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        var mediator = new FakeMediator();
        mediator.Register<GetALaCarteItemsQuery, IReadOnlyList<ALaCarteItemDto>>(_ => [Item(2, "Rántott szelet", ALaCarteCategory.Foetel)]);
        mediator.Register<GetDailyOffersQuery, IReadOnlyList<ALaCarteDailyOfferDto>>(
            _ => [Offer(11, 2, "Rántott szelet", ALaCarteCategory.Foetel, 10, 3)]);
        mediator.Register<SetDailyOfferCommand, Result<ALaCarteDailyOfferDto>>(
            _ => Result.Failure<ALaCarteDailyOfferDto>(ErrorCodes.CapacityBelowReserved, "A keret nem csökkenthető a már lefoglalt darabszám alá."));
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<AdminALaCarteDailyOffer>((ComponentParameterCollectionBuilder<AdminALaCarteDailyOffer> _) => { });
        ClickButtonWithText(cut, "Szerkesztés");
        var capacityField = cut.FindComponent<MudBlazor.MudNumericField<int>>();
        cut.InvokeAsync(() => capacityField.Instance.ValueChanged.InvokeAsync(1)); // below the 3 already reserved

        ClickButtonWithText(cut, "Mentés");

        Assert.Contains("A keret nem csökkenthető", cut.Markup);
        Assert.Contains("Mégse", cut.Markup); // still in edit mode, so the admin can correct it
    }
}
