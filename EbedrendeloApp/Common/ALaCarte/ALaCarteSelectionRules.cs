using EbedrendeloApp.Features.Menus.GetTodayMenuForUser;

namespace EbedrendeloApp.Common.ALaCarte;

/// <summary>Kategóriánként legfeljebb egy kiválasztott tétel (rádiógomb-viselkedés) — a mai menü à la
/// carte kártyarács kiválasztás-szabálya, kiemelve a <c>TodayMenu.razor</c>-ból, hogy a szabály önmagában,
/// Blazor-render nélkül, egy egyszerű unit teszttel is ellenőrizhető legyen.</summary>
public static class ALaCarteSelectionRules
{
    public static HashSet<int> Toggle(IReadOnlySet<int> currentSelection, IReadOnlyList<ALaCarteOfferDto> offers, int toggledItemId)
    {
        var next = new HashSet<int>(currentSelection);

        if (next.Contains(toggledItemId))
        {
            next.Remove(toggledItemId);
            return next;
        }

        var category = offers.First(o => o.ALaCarteItemId == toggledItemId).Category;
        next.RemoveWhere(id => offers.Any(o => o.ALaCarteItemId == id && o.Category == category));
        next.Add(toggledItemId);
        return next;
    }
}
