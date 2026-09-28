using Bunit;
using EbedrendeloApp.Components.Pages.Menus;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Menus.GetTodayMenuForUser;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace EbedrendeloApp.Tests.Components.Menus;

public class ALaCarteOrderConfirmDialogTests : MudBunitContext
{
    private static readonly ALaCarteOfferDto Soup =
        new(1, "Húsleves", ALaCarteCategory.Leves, 600, FreeCount: 5);

    private static readonly ALaCarteOfferDto MainWithSoup =
        new(2, "Rántott hús", ALaCarteCategory.Foetel, 1800, FreeCount: 3, IncludesSoup: true);

    public ALaCarteOrderConfirmDialogTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private async Task<IRenderedComponent<MudDialogProvider>> ShowAsync(params ALaCarteOfferDto[] offers)
    {
        var provider = Render<MudDialogProvider>();
        var dialogService = Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<ALaCarteOrderConfirmDialog>
        {
            { x => x.Offers, offers },
        };
        await provider.InvokeAsync(() =>
            dialogService.ShowAsync<ALaCarteOrderConfirmDialog>("Rendelés megerősítése", parameters));
        return provider;
    }

    [Fact]
    public async Task Lists_every_item_with_its_category_and_formatted_price()
    {
        var provider = await ShowAsync(Soup, MainWithSoup);

        Assert.Contains("Húsleves", provider.Markup);
        Assert.Contains("Rántott hús", provider.Markup);
        Assert.Contains("600 Ft", provider.Markup);
        Assert.Contains("1\u00A0800 Ft", provider.Markup);
    }

    [Fact]
    public async Task Marks_a_main_course_that_comes_with_soup()
    {
        var provider = await ShowAsync(MainWithSoup);

        Assert.Contains("(levessel)", provider.Markup);
    }

    [Fact]
    public async Task Sums_the_items_into_a_total()
    {
        var provider = await ShowAsync(Soup, MainWithSoup);

        // 600 + 1800 — ezresenként tagolva, a HungarianNumberFormat szerint.
        Assert.Contains("2\u00A0400 Ft", provider.Markup);
    }
}
