using Bunit;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Components.Pages.ALaCarte;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.ALaCarte;
using EbedrendeloApp.Features.ALaCarte.GetALaCarteItems;
using EbedrendeloApp.Features.ALaCarte.UpsertALaCarteItem;
using EbedrendeloApp.Tests.TestSupport;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace EbedrendeloApp.Tests.Components.ALaCarte;

public class ALaCarteItemDialogTests : MudBunitContext
{
    public ALaCarteItemDialogTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(7, "Admin Teszt", isAdmin: true));
    }

    private async Task<IRenderedComponent<MudDialogProvider>> OpenAsync(FakeMediator mediator, ALaCarteItemDto? existing = null)
    {
        Services.AddSingleton<IMediator>(mediator);
        var provider = Render<MudDialogProvider>((ComponentParameterCollectionBuilder<MudDialogProvider> _) => { });
        var dialogService = Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<ALaCarteItemDialog>();
        if (existing is not null)
        {
            parameters.Add(x => x.Existing, existing);
        }
        await provider.InvokeAsync(() => dialogService.ShowAsync<ALaCarteItemDialog>("Tétel", parameters));
        return provider;
    }

    [Fact]
    public async Task Saving_a_new_item_sends_the_upsert_command_with_a_null_id()
    {
        var mediator = new FakeMediator();
        mediator.Register<GetALaCarteItemsQuery, IReadOnlyList<ALaCarteItemDto>>(_ => []);
        UpsertALaCarteItemCommand? sentCommand = null;
        mediator.Register<UpsertALaCarteItemCommand, Result<ALaCarteItemDto>>(cmd =>
        {
            sentCommand = cmd;
            return Result.Success(new ALaCarteItemDto(1, cmd.Name, cmd.Category, cmd.PriceHuf, cmd.IsActive, null, null, null, null, null, null, null, null));
        });
        var provider = await OpenAsync(mediator);

        var nameAutocomplete = provider.FindComponent<MudAutocomplete<string>>();
        await provider.InvokeAsync(() => nameAutocomplete.Instance.ValueChanged.InvokeAsync("Rántott sertés szelet"));
        var saveButton = provider.FindAll("button").First(b => b.TextContent.Contains("Mentés"));
        await provider.InvokeAsync(() => saveButton.Click());

        Assert.NotNull(sentCommand);
        Assert.Null(sentCommand!.Id);
        Assert.Equal("Rántott sertés szelet", sentCommand.Name);
    }

    [Fact]
    public async Task Selecting_a_name_matching_an_existing_item_updates_it_instead_of_creating_a_duplicate()
    {
        var mediator = new FakeMediator();
        mediator.Register<GetALaCarteItemsQuery, IReadOnlyList<ALaCarteItemDto>>(
            _ => [new ALaCarteItemDto(9, "Rántott sertés szelet", ALaCarteCategory.Foetel, 1900, true, "1,3", null, null, null, null, null, null, null)]);
        UpsertALaCarteItemCommand? sentCommand = null;
        mediator.Register<UpsertALaCarteItemCommand, Result<ALaCarteItemDto>>(cmd =>
        {
            sentCommand = cmd;
            return Result.Success(new ALaCarteItemDto(9, cmd.Name, cmd.Category, cmd.PriceHuf, cmd.IsActive, null, null, null, null, null, null, null, null));
        });
        var provider = await OpenAsync(mediator);

        var nameAutocomplete = provider.FindComponent<MudAutocomplete<string>>();
        await provider.InvokeAsync(() => nameAutocomplete.Instance.ValueChanged.InvokeAsync("Rántott sertés szelet"));

        Assert.Contains("már szerepel a katalógusban", provider.Markup);

        var saveButton = provider.FindAll("button").First(b => b.TextContent.Contains("Mentés"));
        await provider.InvokeAsync(() => saveButton.Click());

        Assert.NotNull(sentCommand);
        Assert.Equal(9, sentCommand!.Id);
        Assert.Equal(1900, sentCommand.PriceHuf); // prefilled from the matched catalog item
    }

    [Fact]
    public async Task Editing_an_existing_item_prefills_the_fields_and_sends_its_id()
    {
        var existing = new ALaCarteItemDto(5, "Somlói galuska", ALaCarteCategory.Desszert, 750, true, "1,3", null, null, null, null, null, null, null);
        var mediator = new FakeMediator();
        UpsertALaCarteItemCommand? sentCommand = null;
        mediator.Register<UpsertALaCarteItemCommand, Result<ALaCarteItemDto>>(cmd =>
        {
            sentCommand = cmd;
            return Result.Success(existing);
        });
        var provider = await OpenAsync(mediator, existing);

        Assert.Contains("Somlói galuska", provider.Markup);

        var saveButton = provider.FindAll("button").First(b => b.TextContent.Contains("Mentés"));
        await provider.InvokeAsync(() => saveButton.Click());

        Assert.NotNull(sentCommand);
        Assert.Equal(5, sentCommand!.Id);
        Assert.Equal(750, sentCommand.PriceHuf);
    }

    /// <summary>A lista Állapot oszlopa nem kattintható (lásd AdminALaCarteItemsTests), ezért az Aktív
    /// kapcsoló itt, a dialóguson keresztül kell hogy elérhető legyen.</summary>
    [Fact]
    public async Task Toggling_the_active_switch_off_deactivates_the_item_on_save()
    {
        var existing = new ALaCarteItemDto(5, "Somlói galuska", ALaCarteCategory.Desszert, 750, true, null, null, null, null, null, null, null, null);
        var mediator = new FakeMediator();
        UpsertALaCarteItemCommand? sentCommand = null;
        mediator.Register<UpsertALaCarteItemCommand, Result<ALaCarteItemDto>>(cmd =>
        {
            sentCommand = cmd;
            return Result.Success(existing);
        });
        var provider = await OpenAsync(mediator, existing);

        var activeSwitch = provider.FindComponent<MudSwitch<bool>>();
        await provider.InvokeAsync(() => activeSwitch.Instance.ValueChanged.InvokeAsync(false));
        var saveButton = provider.FindAll("button").First(b => b.TextContent.Contains("Mentés"));
        await provider.InvokeAsync(() => saveButton.Click());

        Assert.NotNull(sentCommand);
        Assert.False(sentCommand!.IsActive);
    }

    [Fact]
    public async Task Shows_the_error_message_returned_by_a_failed_save()
    {
        var mediator = new FakeMediator();
        mediator.Register<GetALaCarteItemsQuery, IReadOnlyList<ALaCarteItemDto>>(_ => []);
        mediator.Register<UpsertALaCarteItemCommand, Result<ALaCarteItemDto>>(
            _ => Result.Failure<ALaCarteItemDto>(EbedrendeloApp.Common.Results.ErrorCodes.NotFound, "A tétel nem található."));
        var provider = await OpenAsync(mediator);

        var nameAutocomplete = provider.FindComponent<MudAutocomplete<string>>();
        await provider.InvokeAsync(() => nameAutocomplete.Instance.ValueChanged.InvokeAsync("X"));
        var saveButton = provider.FindAll("button").First(b => b.TextContent.Contains("Mentés"));
        await provider.InvokeAsync(() => saveButton.Click());

        Assert.Contains("A tétel nem található.", provider.Markup);
    }
}
