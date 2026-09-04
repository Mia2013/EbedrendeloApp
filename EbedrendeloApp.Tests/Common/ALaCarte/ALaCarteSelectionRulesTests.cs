using EbedrendeloApp.Common.ALaCarte;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Menus.GetTodayMenuForUser;

namespace EbedrendeloApp.Tests.Common.ALaCarte;

public class ALaCarteSelectionRulesTests
{
    private static readonly ALaCarteOfferDto Main1 = new(1, "Rántott szelet", ALaCarteCategory.Foetel, 1500, 7);
    private static readonly ALaCarteOfferDto Main2 = new(2, "Csirkemell", ALaCarteCategory.Foetel, 1600, 7);
    private static readonly ALaCarteOfferDto Side1 = new(3, "Rizi-bizi", ALaCarteCategory.Koret, 500, 7);
    private static readonly List<ALaCarteOfferDto> Offers = [Main1, Main2, Side1];

    [Fact]
    public void Selecting_an_unselected_item_adds_it()
    {
        var result = ALaCarteSelectionRules.Toggle(new HashSet<int>(), Offers, Main1.ALaCarteItemId);

        Assert.Equal([Main1.ALaCarteItemId], result);
    }

    [Fact]
    public void Selecting_an_already_selected_item_removes_it()
    {
        var current = new HashSet<int> { Main1.ALaCarteItemId };

        var result = ALaCarteSelectionRules.Toggle(current, Offers, Main1.ALaCarteItemId);

        Assert.Empty(result);
    }

    [Fact]
    public void Selecting_another_item_in_the_same_category_replaces_the_previous_one()
    {
        var current = new HashSet<int> { Main1.ALaCarteItemId };

        var result = ALaCarteSelectionRules.Toggle(current, Offers, Main2.ALaCarteItemId);

        Assert.Equal([Main2.ALaCarteItemId], result);
    }

    [Fact]
    public void Selecting_an_item_in_a_different_category_keeps_the_existing_selection()
    {
        var current = new HashSet<int> { Main1.ALaCarteItemId };

        var result = ALaCarteSelectionRules.Toggle(current, Offers, Side1.ALaCarteItemId);

        Assert.Equal([Main1.ALaCarteItemId, Side1.ALaCarteItemId], result.OrderBy(i => i));
    }

    [Fact]
    public void Does_not_mutate_the_set_passed_in()
    {
        var current = new HashSet<int> { Main1.ALaCarteItemId };

        ALaCarteSelectionRules.Toggle(current, Offers, Side1.ALaCarteItemId);

        Assert.Equal([Main1.ALaCarteItemId], current);
    }
}
