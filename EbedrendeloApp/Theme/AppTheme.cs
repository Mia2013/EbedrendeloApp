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
            AppbarBackground = "#594AE2",
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#7C6FEF",
            Success = "#2FBE7A",
            Error = "#EF5350",
            Warning = "#FFC46B",
            Info = "#42A5F5",
        },
    };
}
