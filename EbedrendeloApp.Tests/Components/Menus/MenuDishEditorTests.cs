using Bunit;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Components.Pages.Menus;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Menus.CreateMenuDish;
using EbedrendeloApp.Features.Menus.GetMenuDishSuggestions;
using EbedrendeloApp.Features.Menus.UpdateMenuDish;
using EbedrendeloApp.Tests.TestSupport;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace EbedrendeloApp.Tests.Components.Menus;

public class MenuDishEditorTests : MudBunitContext
{
    private static readonly MenuDishDto ExistingDish = new(
        "Gulyásleves", "1,7", EnergyKcal: 120, Id: 42, Kind: MenuDishKind.Leves);

    private readonly FakeMediator mediator = new();

    public MenuDishEditorTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<IMediator>(mediator);
    }

    [Fact]
    public void An_empty_name_keeps_the_save_button_disabled()
    {
        var cut = Render<MenuDishEditor>();

        var save = cut.FindAll("button").Single(b => b.TextContent.Contains("Mentés"));
        Assert.True(save.HasAttribute("disabled"));
    }

    [Fact]
    public void Edit_mode_prefills_the_fields_and_locks_the_dish_kind()
    {
        // A típus utólag nem változtatható: egy levesből nem lehet főétel, mert a már kiadott
        // menüvariánsok hivatkoznák.
        var cut = Render<MenuDishEditor>(p => p.Add(x => x.Existing, ExistingDish));

        Assert.Equal("Gulyásleves", cut.Find("input").GetAttribute("value"));

        // A típusválasztó a szerkesztő módban le van tiltva.
        var kindInput = cut.FindAll("div.mud-select input").First();
        Assert.True(kindInput.HasAttribute("disabled"));
    }

    [Fact]
    public async Task Saving_a_new_dish_sends_a_create_command_and_reports_the_saved_dish()
    {
        CreateMenuDishCommand? sent = null;
        var saved = new MenuDishDto("Új leves", null, Id: 7, Kind: MenuDishKind.Leves);
        mediator.Register<CreateMenuDishCommand, Result<MenuDishDto>>(c =>
        {
            sent = c;
            return Result.Success(saved);
        });

        MenuDishDto? reported = null;
        var cut = Render<MenuDishEditor>(p => p
            .Add(x => x.OnSaved, dish => reported = dish));

        cut.Find("input").Input("Új leves");
        await cut.Find("button.mud-button-filled-success").ClickAsync(new());

        Assert.NotNull(sent);
        Assert.Equal("Új leves", sent.Name);
        Assert.Equal(MenuDishKind.Leves, sent.Kind);
        Assert.Equal(saved, reported);
    }

    [Fact]
    public async Task Saving_an_existing_dish_sends_an_update_command_with_its_id()
    {
        UpdateMenuDishCommand? sent = null;
        mediator.Register<UpdateMenuDishCommand, Result<MenuDishDto>>(c =>
        {
            sent = c;
            return Result.Success(ExistingDish);
        });

        var cut = Render<MenuDishEditor>(p => p.Add(x => x.Existing, ExistingDish));

        await cut.Find("button.mud-button-filled-success").ClickAsync(new());

        Assert.NotNull(sent);
        Assert.Equal(42, sent.Id);
        Assert.Equal("Gulyásleves", sent.Name);
    }

    [Fact]
    public async Task A_failed_save_shows_the_error_and_does_not_report_a_saved_dish()
    {
        mediator.Register<CreateMenuDishCommand, Result<MenuDishDto>>(
            _ => Result.Failure<MenuDishDto>(ErrorCodes.DuplicateName, "Már létezik ilyen nevű étel."));

        var reported = false;
        var cut = Render<MenuDishEditor>(p => p.Add(x => x.OnSaved, _ => reported = true));

        cut.Find("input").Input("Gulyásleves");
        await cut.Find("button.mud-button-filled-success").ClickAsync(new());

        Assert.Contains("Már létezik ilyen nevű étel.", cut.Markup);
        Assert.False(reported);
    }

    [Fact]
    public async Task Cancelling_raises_the_callback_without_touching_the_mediator()
    {
        var cancelled = false;
        var cut = Render<MenuDishEditor>(p => p.Add(x => x.OnCancelled, () => cancelled = true));

        await cut.FindAll("button").Single(b => b.TextContent.Contains("Mégse")).ClickAsync(new());

        Assert.True(cancelled);
    }
}
