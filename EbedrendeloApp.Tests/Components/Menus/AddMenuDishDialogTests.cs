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
using MudBlazor;
using MudBlazor.Services;

namespace EbedrendeloApp.Tests.Components.Menus;

public class AddMenuDishDialogTests : MudBunitContext
{
    public AddMenuDishDialogTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task With_no_parameters_defaults_to_creating_a_soup()
    {
        CreateMenuDishCommand? sentCommand = null;
        var mediator = new FakeMediator();
        mediator.Register<CreateMenuDishCommand, Result<MenuDishDto>>(cmd =>
        {
            sentCommand = cmd;
            return Result.Success(new MenuDishDto(cmd.Name, null, Kind: cmd.Kind));
        });
        Services.AddSingleton<IMediator>(mediator);

        var provider = Render<MudDialogProvider>((ComponentParameterCollectionBuilder<MudDialogProvider> _) => { });
        var dialogService = Services.GetRequiredService<IDialogService>();
        await provider.InvokeAsync(() => dialogService.ShowAsync<AddMenuDishDialog>("Új étel felvétele"));

        Assert.Contains("Új étel hozzáadása", provider.Markup);

        var nameInput = FindInputByLabel(provider, "Név");
        await provider.InvokeAsync(() => nameInput.Input("Gulyásleves"));

        var saveButton = provider.FindAll("button").First(b => b.TextContent.Contains("Mentés"));
        await provider.InvokeAsync(() => saveButton.Click());

        Assert.NotNull(sentCommand);
        Assert.Equal(MenuDishKind.Leves, sentCommand!.Kind);
        Assert.Equal("Gulyásleves", sentCommand.Name);
    }

    [Fact]
    public async Task A_duplicate_name_shows_the_server_error_and_keeps_the_dialog_open()
    {
        var mediator = new FakeMediator();
        mediator.Register<CreateMenuDishCommand, Result<MenuDishDto>>(
            _ => Result.Failure<MenuDishDto>(ErrorCodes.DuplicateName, "Már létezik ilyen nevű étel."));
        Services.AddSingleton<IMediator>(mediator);

        var provider = Render<MudDialogProvider>((ComponentParameterCollectionBuilder<MudDialogProvider> _) => { });
        var dialogService = Services.GetRequiredService<IDialogService>();
        await provider.InvokeAsync(() => dialogService.ShowAsync<AddMenuDishDialog>("Új étel felvétele"));

        var nameInput = FindInputByLabel(provider, "Név");
        await provider.InvokeAsync(() => nameInput.Input("Gulyásleves"));

        var saveButton = provider.FindAll("button").First(b => b.TextContent.Contains("Mentés"));
        await provider.InvokeAsync(() => saveButton.Click());

        Assert.Contains("Már létezik ilyen nevű étel.", provider.Markup);
    }

    [Fact]
    public async Task Edit_mode_prefills_the_name_and_type_from_the_existing_dish()
    {
        Services.AddSingleton<IMediator>(new FakeMediator());

        var provider = Render<MudDialogProvider>((ComponentParameterCollectionBuilder<MudDialogProvider> _) => { });
        var dialogService = Services.GetRequiredService<IDialogService>();
        var existing = new MenuDishDto("Rántott hús", "3", EnergyKcal: 250, Id: 5, Kind: MenuDishKind.Foetel);
        var parameters = new DialogParameters<AddMenuDishDialog> { { x => x.Existing, existing } };

        await provider.InvokeAsync(() => dialogService.ShowAsync<AddMenuDishDialog>("Szerkesztés", parameters));

        Assert.Contains("főétel szerkesztése", provider.Markup);
        var nameInput = FindInputByLabel(provider, "Név");
        Assert.Equal("Rántott hús", nameInput.GetAttribute("value"));
    }

    [Fact]
    public async Task Edit_mode_saving_sends_the_update_command_for_the_existing_id()
    {
        UpdateMenuDishCommand? sentCommand = null;
        var mediator = new FakeMediator();
        mediator.Register<UpdateMenuDishCommand, Result<MenuDishDto>>(cmd =>
        {
            sentCommand = cmd;
            return Result.Success(new MenuDishDto(cmd.Name, null, Id: cmd.Id));
        });
        Services.AddSingleton<IMediator>(mediator);

        var provider = Render<MudDialogProvider>((ComponentParameterCollectionBuilder<MudDialogProvider> _) => { });
        var dialogService = Services.GetRequiredService<IDialogService>();
        var existing = new MenuDishDto("Gulyásleves", "9", Id: 5, Kind: MenuDishKind.Leves);
        var parameters = new DialogParameters<AddMenuDishDialog> { { x => x.Existing, existing } };
        await provider.InvokeAsync(() => dialogService.ShowAsync<AddMenuDishDialog>("Szerkesztés", parameters));

        var saveButton = provider.FindAll("button").First(b => b.TextContent.Contains("Mentés"));
        await provider.InvokeAsync(() => saveButton.Click());

        Assert.NotNull(sentCommand);
        Assert.Equal(5, sentCommand!.Id);
        Assert.Equal("Gulyásleves", sentCommand.Name);
    }

    [Fact]
    public async Task Cancel_never_sends_the_create_command()
    {
        var createCalled = false;
        var mediator = new FakeMediator();
        mediator.Register<CreateMenuDishCommand, Result<MenuDishDto>>(_ =>
        {
            createCalled = true;
            return Result.Success(new MenuDishDto("x", null));
        });
        Services.AddSingleton<IMediator>(mediator);

        var provider = Render<MudDialogProvider>((ComponentParameterCollectionBuilder<MudDialogProvider> _) => { });
        var dialogService = Services.GetRequiredService<IDialogService>();
        await provider.InvokeAsync(() => dialogService.ShowAsync<AddMenuDishDialog>("Új étel felvétele"));

        var cancelButton = provider.FindAll("button").First(b => b.TextContent.Contains("Mégse"));
        await provider.InvokeAsync(() => cancelButton.Click());

        Assert.False(createCalled);
    }

    private static AngleSharp.Dom.IElement FindInputByLabel(IRenderedComponent<MudDialogProvider> provider, string labelText)
        => provider.FindAll("div.mud-input-control")
            .First(d => d.QuerySelector("label")?.TextContent.Contains(labelText) == true)
            .QuerySelector("input")!;
}
