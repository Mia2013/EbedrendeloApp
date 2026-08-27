namespace EbedrendeloApp.Common.Allergens;

public sealed record AllergenOption(int Number, string Name);

/// <summary>
/// The 14 EU-declared allergens (1169/2011/EU Annex II), fixed and numbered — the numbering itself is
/// what Hungarian menus conventionally print next to a dish, so it's part of the data, not just an Id.
/// <see cref="Domain.Entities.MenuDish.Allergens"/> stores a selection from this catalog as a
/// comma-separated list of <see cref="AllergenOption.Number"/> values (e.g. "1,7,9") — <see cref="Format"/>
/// turns that back into the "szám – név" display text.
/// </summary>
public static class AllergenCatalog
{
    public static readonly IReadOnlyList<AllergenOption> All =
    [
        new(1, "Glutén"),
        new(2, "Rákfélék"),
        new(3, "Tojás"),
        new(4, "Hal"),
        new(5, "Földimogyoró"),
        new(6, "Szója"),
        new(7, "Tej (laktóz)"),
        new(8, "Diófélék"),
        new(9, "Zeller"),
        new(10, "Mustár"),
        new(11, "Szezámmag"),
        new(12, "Kén-dioxid/szulfitok"),
        new(13, "Csillagfürt (lupin)"),
        new(14, "Puhatestűek"),
    ];

    private static readonly Dictionary<int, string> NameByNumber = All.ToDictionary(a => a.Number, a => a.Name);

    public static HashSet<int> Parse(string? commaSeparatedNumbers)
    {
        if (string.IsNullOrWhiteSpace(commaSeparatedNumbers))
        {
            return [];
        }

        return commaSeparatedNumbers
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var number) ? number : (int?)null)
            .Where(number => number is not null)
            .Select(number => number!.Value)
            .ToHashSet();
    }

    public static string? Serialize(IEnumerable<int> numbers)
    {
        var sorted = numbers.Distinct().OrderBy(n => n).ToList();
        return sorted.Count == 0 ? null : string.Join(",", sorted);
    }

    public static string? Format(string? commaSeparatedNumbers) => FormatNumbers(Parse(commaSeparatedNumbers));

    /// <summary>Compact "1, 7, 9" form for tight list layouts — pair it with a tooltip showing <see cref="Format"/>'s
    /// full "szám – név" text so the names aren't lost, just deferred until hover.</summary>
    public static string? FormatNumbersOnly(string? commaSeparatedNumbers)
    {
        var numbers = Parse(commaSeparatedNumbers).OrderBy(n => n).ToList();
        return numbers.Count == 0 ? null : string.Join(", ", numbers);
    }

    public static string? FormatNumbers(IEnumerable<int> numbers)
    {
        var labels = numbers
            .Distinct()
            .OrderBy(n => n)
            .Select(n => NameByNumber.TryGetValue(n, out var name) ? $"{n} – {name}" : n.ToString())
            .ToList();

        return labels.Count == 0 ? null : string.Join(", ", labels);
    }
}
