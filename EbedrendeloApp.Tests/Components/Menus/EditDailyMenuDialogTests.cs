using Bunit;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Components.Pages.Menus;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Menus;
using EbedrendeloApp.Features.Menus.DeleteMenuVariant;
using EbedrendeloApp.Features.Menus.GetDailyMenu;
using EbedrendeloApp.Features.Menus.GetMenuDishSuggestions;
using EbedrendeloApp.Features.Menus.UpsertDailyMenu;
using EbedrendeloApp.Tests.TestSupport;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace EbedrendeloApp.Tests.Components.Menus;

public class EditDailyMenuDialogTests : MudBunitContext
{
    private static readonly DateOnly Date = new(2026, 8, 10);

    public EditDailyMenuDialogTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(7, "Admin Teszt", isAdmin: true));
    }

    private static FakeMediator BaseMediator(DailyMenuDto? existing = null)
    {
        var mediator = new FakeMediator();
        mediator.Register<GetMenuDishSuggestionsQuery, MenuDishSuggestionsDto>(_ => new MenuDishSuggestionsDto([], []));
        mediator.Register<GetDailyMenuQuery, DailyMenuDto?>(_ => existing);
        return mediator;
    }

    private async Task<IRenderedComponent<MudDialogProvider>> OpenAsync(FakeMediator mediator)
    {
        Services.AddSingleton<IMediator>(mediator);
        var provider = Render<MudDialogProvider>((ComponentParameterCollectionBuilder<MudDialogProvider> _) => { });
        var dialogService = Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<EditDailyMenuDialog> { { x => x.Date, Date } };
        await provider.InvokeAsync(() => dialogService.ShowAsync<EditDailyMenuDialog>("Nap menüjének szerkesztése", parameters));
        return provider;
    }

    [Fact]
    public async Task With_no_existing_menu_defaults_to_three_empty_ABC_variants()
    {
        var provider = await OpenAsync(BaseMediator());

        Assert.Equal(3, provider.FindAll("button[title='Variáns törlése']").Count);
    }

    [Fact]
    public async Task Saving_sends_the_upsert_command_for_the_dialogs_date()
    {
        var mediator = BaseMediator(new DailyMenuDto(Date, IsPublished: false, Note: null, Variants: [new MenuVariantDto("A", "Rántott hús", null, 0)]));
        UpsertDailyMenuCommand? sentCommand = null;
        mediator.Register<UpsertDailyMenuCommand, Result<int>>(cmd =>
        {
            sentCommand = cmd;
            return Result.Success(1);
        });
        var provider = await OpenAsync(mediator);

        var saveButton = provider.FindAll("button").First(b => b.TextContent.Contains("Mentés"));
        await provider.InvokeAsync(() => saveButton.Click());

        Assert.NotNull(sentCommand);
        Assert.Equal(Date, sentCommand!.Date);
        Assert.Equal(7, sentCommand.PerformedByUserId);
        var variant = Assert.Single(sentCommand.Variants);
        Assert.Equal("A", variant.Code);
    }

    [Fact]
    public async Task Cancel_closes_without_calling_the_server()
    {
        var mediator = BaseMediator();
        var saveCalled = false;
        mediator.Register<UpsertDailyMenuCommand, Result<int>>(_ => { saveCalled = true; return Result.Success(1); });
        var provider = await OpenAsync(mediator);

        var cancelButton = provider.FindAll("button").First(b => b.TextContent.Contains("Mégse"));
        await provider.InvokeAsync(() => cancelButton.Click());

        Assert.False(saveCalled);
        Assert.Empty(provider.FindAll("div.mud-dialog"));
    }

    [Fact]
    public async Task Removing_a_persisted_variant_opens_the_confirm_dialog_and_sends_the_delete_command()
    {
        var mediator = BaseMediator(new DailyMenuDto(Date, true, null, [new MenuVariantDto("A", "Rántott hús", null, 0)]));
        DeleteMenuVariantCommand? sentCommand = null;
        mediator.Register<DeleteMenuVariantCommand, Result>(cmd =>
        {
            sentCommand = cmd;
            return Result.Success();
        });
        var provider = await OpenAsync(mediator);

        var deleteButton = provider.FindAll("button[title='Variáns törlése']").First();
        await provider.InvokeAsync(() => deleteButton.Click());

        Assert.Contains("Variáns törlése", provider.Markup);
        var confirmButton = provider.FindAll("button").First(b => b.TextContent.Contains("Törlés"));
        await provider.InvokeAsync(() => confirmButton.Click());

        Assert.NotNull(sentCommand);
        Assert.Equal(Date, sentCommand!.Date);
        Assert.Equal("A", sentCommand.VariantCode);
    }

    [Fact]
    public async Task Adding_and_removing_an_unpersisted_variant_never_calls_the_server()
    {
        var mediator = BaseMediator();
        var deleteCalled = false;
        mediator.Register<DeleteMenuVariantCommand, Result>(_ => { deleteCalled = true; return Result.Success(); });
        var provider = await OpenAsync(mediator);

        // Üres napon alapból 3 (A/B/C) variáns van felkínálva — ehhez adunk egy negyediket, majd pont
        // azt távolítjuk el.
        var defaultRowCount = provider.FindAll("button[title='Variáns törlése']").Count;

        var addButton = provider.FindAll("button").First(b => b.TextContent.Contains("Variáns hozzáadása"));
        await provider.InvokeAsync(() => addButton.Click());

        var deleteButton = provider.FindAll("button[title='Variáns törlése']").Last();
        await provider.InvokeAsync(() => deleteButton.Click());

        Assert.False(deleteCalled);
        Assert.Equal(defaultRowCount, provider.FindAll("button[title='Variáns törlése']").Count);
    }

    [Fact]
    public async Task Saving_an_untouched_row_preserves_its_previously_selected_allergens()
    {
        var mediator = BaseMediator(new DailyMenuDto(Date, true, null, [new MenuVariantDto("A", "Gulyásleves", null, 0, SoupAllergens: "7,9")]));
        UpsertDailyMenuCommand? sentCommand = null;
        mediator.Register<UpsertDailyMenuCommand, Result<int>>(cmd =>
        {
            sentCommand = cmd;
            return Result.Success(1);
        });
        var provider = await OpenAsync(mediator);

        var saveButton = provider.FindAll("button").First(b => b.TextContent.Contains("Mentés"));
        await provider.InvokeAsync(() => saveButton.Click());

        Assert.NotNull(sentCommand);
        var variant = Assert.Single(sentCommand!.Variants);
        Assert.Equal("7,9", variant.SoupAllergens);
    }

    [Fact]
    public async Task Clicking_hozzaad_opens_an_inline_new_dish_form_without_opening_a_second_dialog()
    {
        var provider = await OpenAsync(BaseMediator());

        Assert.Single(provider.FindAll("div.mud-dialog"));

        var addButton = provider.FindAll("button").First(b => b.TextContent.Contains("Hozzáad") && !b.TextContent.Contains("Variáns"));
        await provider.InvokeAsync(() => addButton.Click());

        // Az inline űrlap ugyanabban a dialógusban jelenik meg — nem nyílik második `MudDialog` felület.
        Assert.Single(provider.FindAll("div.mud-dialog"));
        var nameInputs = provider.FindAll("div.mud-input-control").Where(d => d.QuerySelector("label")?.TextContent.Contains("Név") == true);
        Assert.NotEmpty(nameInputs);
    }

    [Fact]
    public async Task Clicking_the_edit_pencil_on_an_existing_soup_opens_an_inline_form_without_opening_a_second_dialog()
    {
        // A szerkesztés-ceruza csak akkor jelenik meg, ha a leves egyeztethető a katalógussal (van Id-je) —
        // ezért itt a katalógusban is szerepeltetni kell ugyanazt a nevű ételt.
        var mediator = new FakeMediator();
        mediator.Register<GetMenuDishSuggestionsQuery, MenuDishSuggestionsDto>(
            _ => new MenuDishSuggestionsDto([new MenuDishDto("Gulyásleves", "9", Id: 42, Kind: MenuDishKind.Leves)], []));
        mediator.Register<GetDailyMenuQuery, DailyMenuDto?>(
            _ => new DailyMenuDto(Date, true, null, [new MenuVariantDto("A", "Gulyásleves", null, 0, SoupAllergens: "9")]));
        var provider = await OpenAsync(mediator);

        var editSoupButton = provider.FindAll("button[title='Leves adatainak szerkesztése']").First();
        await provider.InvokeAsync(() => editSoupButton.Click());

        Assert.Single(provider.FindAll("div.mud-dialog"));
        var nameInput = provider.FindAll("div.mud-input-control")
            .First(d => d.QuerySelector("label")?.TextContent.Contains("Név") == true)
            .QuerySelector("input");
        Assert.Equal("Gulyásleves", nameInput!.GetAttribute("value"));
    }
}
