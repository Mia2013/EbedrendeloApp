using MudBlazor;

namespace EbedrendeloApp.Theme;

/// <summary>Palette lifted from the approved "Epic 1 Naptár UI" design canvas.</summary>
public static class AppTheme
{
    public static readonly MudTheme Default = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#594AE2",
            Success = "#0CAF60",
            Error = "#F44336",
            Warning = "#FFB545",
            Info = "#2196F3",
            Background = "#f5f5f5",
            // A dark-témás Primary (#7C6FEF) egy fokkal halványabb, mint a saját #594AE2 — a navigáció
            // ezt kapja háttérként, hogy ne domináljon annyira, miközben még mindig egyértelműen a márka
            // színe marad.
            AppbarBackground = "#7C6FEF",
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#7C6FEF",
            Success = "#2FBE7A",
            Error = "#EF5350",
            Warning = "#FFC46B",
            Info = "#42A5F5",
        },
        LayoutProperties = new LayoutProperties
        {
            // Az alapértelmezett 64px-es appbar-magasság mellé közvetlenül belógott a naptár sticky
            // fejléc-sora — ez a plusz pár pixel ad neki levegőt, hogy scrollozáskor ne ugorjon rá a
            // fölötte lévő címsorra.
            AppbarHeight = "72px",
        },
    };
}
