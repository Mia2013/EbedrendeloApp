using System.Globalization;
using EbedrendeloApp.Common.Formatting;
using EbedrendeloApp.Features.Admin.GetAdminDashboard;
using MudBlazor;

namespace EbedrendeloApp.Common.Admin;

/// <summary>
/// A „Mai teendők" sorainak megjelenítése: a handler adatot ad vissza (mi hiányzik, mennyi, mikorra),
/// a felirat, az ikon, a szín és a cél-oldal itt egy helyen dől el — ugyanaz a minta, mint az
/// <see cref="ALaCarte.ALaCarteCategoryDisplay"/>-nél.
/// </summary>
public static class AdminTodoDisplay
{
    private static readonly CultureInfo HungarianCulture = CultureInfo.GetCultureInfo("hu-HU");

    /// <summary>Ennyi dátumot sorolunk fel a hiányzó menüknél; a többit a „+N nap" zárja.</summary>
    private const int MaxListedDates = 3;

    public static string Title(AdminTodoDto todo) => todo.Kind switch
    {
        AdminTodoKind.MissingDailyMenu => $"{todo.Count} munkanapra nincs feltöltve napi menü",
        AdminTodoKind.MissingALaCarteOffer => "A következő munkanapra nincs à la carte kínálat",
        AdminTodoKind.KitchenDayOpen => "A mai napot még nem zárta le a konyha",
        AdminTodoKind.UnpaidInvoices => $"{todo.Count} kiállított számla fizetetlen",
        _ => string.Empty,
    };

    public static string Detail(AdminTodoDto todo) => todo.Kind switch
    {
        AdminTodoKind.MissingDailyMenu => MissingMenuDetail(todo.Dates),
        AdminTodoKind.MissingALaCarteOffer => "Amíg nincs kiajánlott tétel, aznap nem lehet à la carte rendelni.",
        AdminTodoKind.KitchenDayOpen => $"{HungarianNumberFormat.Number(todo.Count)} tétel vár összesítésre.",
        AdminTodoKind.UnpaidInvoices => $"{HungarianNumberFormat.Huf(todo.AmountHuf)} kintlévőség.",
        _ => string.Empty,
    };

    public static string Icon(AdminTodoKind kind) => kind switch
    {
        AdminTodoKind.MissingDailyMenu => Icons.Material.Filled.Warning,
        AdminTodoKind.MissingALaCarteOffer => Icons.Material.Filled.Fastfood,
        AdminTodoKind.KitchenDayOpen => Icons.Material.Filled.Schedule,
        AdminTodoKind.UnpaidInvoices => Icons.Material.Filled.CreditCard,
        _ => Icons.Material.Filled.Info,
    };

    /// <summary>A szín a sürgősséget kódolja: a pénzügyi kintlévőség hibaszínt kap, a hiányzó menü
    /// figyelmeztetést, a nyitott konyhai nap pedig csak információt.</summary>
    public static Color Color(AdminTodoKind kind) => kind switch
    {
        AdminTodoKind.MissingDailyMenu => MudBlazor.Color.Warning,
        AdminTodoKind.MissingALaCarteOffer => MudBlazor.Color.Warning,
        AdminTodoKind.KitchenDayOpen => MudBlazor.Color.Info,
        AdminTodoKind.UnpaidInvoices => MudBlazor.Color.Error,
        _ => MudBlazor.Color.Default,
    };

    public static string Href(AdminTodoKind kind) => kind switch
    {
        AdminTodoKind.MissingDailyMenu => "etlap",
        AdminTodoKind.MissingALaCarteOffer => "alacarte-napi-kinalat",
        AdminTodoKind.KitchenDayOpen => "konyhai-osszesito",
        AdminTodoKind.UnpaidInvoices => "szamlak",
        _ => string.Empty,
    };

    public static string ActionLabel(AdminTodoKind kind) => kind switch
    {
        AdminTodoKind.MissingDailyMenu => "Étlap",
        AdminTodoKind.MissingALaCarteOffer => "Kínálat",
        AdminTodoKind.KitchenDayOpen => "Konyha",
        AdminTodoKind.UnpaidInvoices => "Számlák",
        _ => "Megnyitás",
    };

    /// <summary>Rövid, magyar dátumfelsorolás — „szept. 15., 16., 23."; a hónapot csak akkor
    /// ismételjük, ha vált.</summary>
    private static string MissingMenuDetail(IReadOnlyList<DateOnly> dates)
    {
        if (dates.Count == 0)
        {
            return string.Empty;
        }

        var listed = dates.Take(MaxListedDates).ToList();
        var parts = new List<string>();
        var lastMonth = 0;

        foreach (var date in listed)
        {
            parts.Add(date.Month == lastMonth
                ? date.ToString("d.", HungarianCulture)
                : date.ToString("MMM d.", HungarianCulture));
            lastMonth = date.Month;
        }

        var text = string.Join(" ", parts);
        return dates.Count > listed.Count
            ? $"{text} és további {dates.Count - listed.Count} nap"
            : text;
    }
}
