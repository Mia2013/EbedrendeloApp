using EbedrendeloApp.Domain.Enums;
using MudBlazor;

namespace EbedrendeloApp.Common.ALaCarte;

/// <summary>
/// Shared category → label/icon mapping — used by every à la carte admin page and by
/// TodayMenu.razor, which previously each carried an identical private switch expression.
/// </summary>
public static class ALaCarteCategoryDisplay
{
    public static string Name(ALaCarteCategory category) => category switch
    {
        ALaCarteCategory.Leves => "Leves",
        ALaCarteCategory.Foetel => "Főétel",
        ALaCarteCategory.Koret => "Köret",
        ALaCarteCategory.Desszert => "Desszert",
        ALaCarteCategory.Ontet => "Öntet",
        _ => category.ToString(),
    };

    public static string Icon(ALaCarteCategory category) => category switch
    {
        ALaCarteCategory.Leves => Icons.Material.Filled.SoupKitchen,
        ALaCarteCategory.Foetel => Icons.Material.Filled.DinnerDining,
        ALaCarteCategory.Koret => Icons.Material.Filled.RiceBowl,
        ALaCarteCategory.Desszert => Icons.Material.Filled.Icecream,
        ALaCarteCategory.Ontet => Icons.Material.Filled.WaterDrop,
        _ => Icons.Material.Filled.Fastfood,
    };
}
