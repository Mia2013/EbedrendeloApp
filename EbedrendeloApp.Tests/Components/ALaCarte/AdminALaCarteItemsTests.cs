using Bunit;
using Bunit.TestDoubles;
using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Components.Pages.ALaCarte;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.ALaCarte;
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

    /// <summary>Az állapot oszlop csak megjelenít, nem kattintható — a Aktív/Inaktív váltás kizárólag a
    /// szerkesztő dialóguson (a MudSwitch-en) keresztül történhet, hogy admin ne tudja véletlen kattintással
    /// kivezetni/visszaaktiválni egy tételt a lista áttekintése közben. Lásd <see cref="ALaCarteItemDialog"/>.</summary>
    [Fact]
    public void Clicking_the_status_chip_does_not_change_the_items_state()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        var mediator = new FakeMediator();
        var loadCount = 0;
        mediator.Register<GetALaCarteItemsQuery, IReadOnlyList<ALaCarteItemDto>>(_ =>
        {
            loadCount++;
            return [Item(1, "Rántott sertés szelet", ALaCarteCategory.Foetel)];
        });
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<AdminALaCarteItems>((ComponentParameterCollectionBuilder<AdminALaCarteItems> _) => { });
        var statusChip = cut.FindAll("td .mud-chip").First(c => c.TextContent.Trim() == "Aktív");
        cut.InvokeAsync(() => statusChip.Click());

        Assert.Equal(1, loadCount);
        Assert.Contains("Aktív", cut.Markup);
    }

    [Fact]
    public void Groups_the_rows_by_category_with_a_group_header_per_category()
    {
        Services.AddSingleton<ICurrentUser>(new FakeCurrentUser(1, "Admin Teszt", isAdmin: true));
        var mediator = new FakeMediator();
        mediator.Register<GetALaCarteItemsQuery, IReadOnlyList<ALaCarteItemDto>>(
            _ => [Item(1, "Somlói galuska", ALaCarteCategory.Desszert), Item(2, "Rántott sertés szelet", ALaCarteCategory.Foetel)]);
        Services.AddSingleton<IMediator>(mediator);

        var cut = Render<AdminALaCarteItems>((ComponentParameterCollectionBuilder<AdminALaCarteItems> _) => { });
        var tableMarkup = cut.Find("table").InnerHtml;

        var foetelHeaderIndex = tableMarkup.IndexOf("Főétel", StringComparison.Ordinal);
        var desszertHeaderIndex = tableMarkup.IndexOf("Desszert", StringComparison.Ordinal);
        var szeletRowIndex = tableMarkup.IndexOf("Rántott sertés szelet", StringComparison.Ordinal);
        var galuskaRowIndex = tableMarkup.IndexOf("Somlói galuska", StringComparison.Ordinal);

        Assert.True(foetelHeaderIndex >= 0 && foetelHeaderIndex < szeletRowIndex, "A Főétel csoportfejlécnek a Főétel sorai előtt kell állnia.");
        Assert.True(desszertHeaderIndex >= 0 && desszertHeaderIndex < galuskaRowIndex, "A Desszert csoportfejlécnek a Desszert sorai előtt kell állnia.");
        Assert.True(szeletRowIndex < desszertHeaderIndex, "A Főétel kategóriának (enum sorrend szerint) a Desszert előtt kell állnia.");
    }
}
