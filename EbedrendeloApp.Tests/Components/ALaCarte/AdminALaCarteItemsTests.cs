using Bunit;
using Bunit.TestDoubles;
using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Components.Pages.ALaCarte;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.ALaCarte;
using EbedrendeloApp.Features.ALaCarte.DeactivateALaCarteItem;
using EbedrendeloApp.Features.ALaCarte.GetALaCarteItems;
using EbedrendeloApp.Tests.TestSupport;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace EbedrendeloApp.Tests.Components.ALaCarte;

public class AdminALaCarteItemsTests : MudBunitContext
{
    public AdminALaCarteItemsTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private static ALaCarteItemDto Item(int id, string name, ALaCarteCategory category, bool isActive = true) =>
        new(id, name, category, 1000, isActive, null, null, null, null, null, null, null, null);

    [Fact]
    public void Redirects_non_admin_users_to_the_today_menu_page()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(2, "Dolgozó Teszt", isAdmin: false));
        Services.AddSingleton<IMediator>(new FakeMediator());

        Render<AdminALaCarteItems>((ComponentParameterCollectionBuilder<AdminALaCarteItems> _) => { });

        var navigationManager = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/mai-menu", navigationManager.Uri);
    }

    [Fact]
    public void Lists_the_items_returned_by_the_query()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        var mediator = new FakeMediator();
        mediator.Register<GetALaCarteItemsQuery, IReadOnlyList<ALaCarteItemDto>>(
            _ => [Item(1, "Rántott sertés szelet", ALaCarteCategory.Foetel), Item(2, "Somlói galuska", ALaCarteCategory.Desszert, isActive: false)]);
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<AdminALaCarteItems>((ComponentParameterCollectionBuilder<AdminALaCarteItems> _) => { });

        Assert.Contains("Rántott sertés szelet", cut.Markup);
        Assert.Contains("Somlói galuska", cut.Markup);
        Assert.Contains("Inaktív", cut.Markup);
    }

    [Fact]
    public void Deactivating_an_item_sends_the_command_and_reloads_the_list()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        var mediator = new FakeMediator();
        var loadCount = 0;
        mediator.Register<GetALaCarteItemsQuery, IReadOnlyList<ALaCarteItemDto>>(_ =>
        {
            loadCount++;
            return loadCount == 1 ? [Item(1, "Rántott sertés szelet", ALaCarteCategory.Foetel)] : [Item(1, "Rántott sertés szelet", ALaCarteCategory.Foetel, isActive: false)];
        });
        DeactivateALaCarteItemCommand? sentCommand = null;
        mediator.Register<DeactivateALaCarteItemCommand, EbedrendeloApp.Common.Results.Result>(cmd =>
        {
            sentCommand = cmd;
            return EbedrendeloApp.Common.Results.Result.Success();
        });
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<AdminALaCarteItems>((ComponentParameterCollectionBuilder<AdminALaCarteItems> _) => { });
        var deactivateButton = cut.Find("button[title='Kivezetés']");
        cut.InvokeAsync(() => deactivateButton.Click());

        Assert.NotNull(sentCommand);
        Assert.Equal(1, sentCommand!.Id);
        Assert.Equal(2, loadCount);
    }
}
