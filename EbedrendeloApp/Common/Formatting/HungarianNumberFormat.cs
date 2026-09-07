using System.Globalization;

namespace EbedrendeloApp.Common.Formatting;

/// <summary>
/// Ezreselválasztós szám-/pénzösszeg-megjelenítés a táblázatokhoz — lásd a
/// <c>mudblazor-ui-first</c> skill "Numeric &amp; Money Columns" szabályát: minden pénz/szám
/// cella jobbra igazított és ezresével tagolt (pl. 1000000 helyett 1 000 000).
/// </summary>
public static class HungarianNumberFormat
{
    private static readonly CultureInfo HungarianCulture = CultureInfo.GetCultureInfo("hu-HU");

    /// <summary>Ezresével tagolt szám, tizedesjegy nélkül (pl. 1000000 → "1 000 000").</summary>
    public static string Number(int value) => value.ToString("N0", HungarianCulture);

    /// <summary>Ezresével tagolt szám, tizedesjegy nélkül (pl. 1000000 → "1 000 000").</summary>
    public static string Number(int? value) => value is null ? string.Empty : Number(value.Value);

    /// <summary>Ezresével tagolt forintösszeg "Ft" felirattal (pl. 1000000 → "1 000 000 Ft").</summary>
    public static string Huf(int amountHuf) => $"{Number(amountHuf)} Ft";

    /// <summary>Ezresével tagolt forintösszeg "Ft" felirattal (pl. 1000000 → "1 000 000 Ft").</summary>
    public static string Huf(int? amountHuf) => amountHuf is null ? string.Empty : Huf(amountHuf.Value);
}
